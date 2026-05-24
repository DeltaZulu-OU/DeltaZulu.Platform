namespace Hunting.Tests.Translation;

using Hunting.Core.Catalog;
using Hunting.Core.DuckDbSql;
using Hunting.Core.Policy;
using Hunting.Core.Schema.Definitions;
using Hunting.Data;

/// <summary>
/// Full end-to-end tests: KQL string → Translate → Emit → Execute against
/// DuckDB with real mock data. These prove the entire vertical slice works.
///
/// Each test writes a realistic hunting query, runs it through the complete
/// pipeline, and validates both the execution success and result content.
/// </summary>
[TestClass]
public sealed class EndToEndPipelineTests
{
    private static QueryRuntime _runtime = null!;
    private static DuckDbConnectionFactory _factory = null!;

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        _factory = new DuckDbConnectionFactory("DataSource=:memory:");

        // Build schema
        var emitter = new SchemaEmitter();
        var applier = new SchemaApplier(_factory);
        var ddl = emitter.EmitAll(
            rawTables: [DeviceProcessEventsSchema.RawWindowsEventJson],
            internalTables: [],
            parserViews: [DeviceProcessEventsSchema.SysmonProcessCreate],
            canonicalViews: [DeviceProcessEventsSchema.View]);
        applier.ApplyStatements(ddl);
        applier.ExecuteRaw(MockDataSeeder.GetSeedSql());

        // Build runtime
        var catalog = new ApprovedViewCatalog();
        catalog.Register(DeviceProcessEventsSchema.View);
        // Enable developer mode so tests can assert GeneratedSql is exposed for debugging
        _runtime = new QueryRuntime(catalog, _factory, defaultLimit: 10_000, developerMode: true);
    }

    [ClassCleanup]
    public static void Cleanup() => _factory.Dispose();

    // ─── Vertical slice query ───────────────────────────────────────

    [TestMethod]
    [Description("Section 19 vertical slice: where + project + take")]
    public void VerticalSlice()
    {
        var result = _runtime.Execute(
            """
            DeviceProcessEvents
            | where FileName == "cmd.exe"
            | project Timestamp, DeviceName, ProcessCommandLine
            | take 20
            """);

        AssertSuccess(result);
        Assert.IsGreaterThanOrEqualTo(2, result.RowCount, $"Expected at least 2 cmd.exe rows, got {result.RowCount}");
        Assert.AreEqual(3, result.ColumnCount, "project should yield 3 columns");
        Assert.AreEqual("Timestamp", result.Columns[0].Name);
        Assert.AreEqual("DeviceName", result.Columns[1].Name);
        Assert.AreEqual("ProcessCommandLine", result.Columns[2].Name);
    }

    // ─── Hunting scenarios ──────────────────────────────────────────

    [TestMethod]
    [Description("Hunt: find all process events")]
    public void Hunt_AllEvents()
    {
        var result = _runtime.Execute("DeviceProcessEvents | take 100");
        AssertSuccess(result);
        Assert.AreEqual(20, result.RowCount);
    }

    [TestMethod]
    [Description("Hunt: count events by device")]
    public void Hunt_CountByDevice()
    {
        var result = _runtime.Execute(
            "DeviceProcessEvents | summarize count() by DeviceName | sort by count_ desc");
        AssertSuccess(result);
        Assert.IsGreaterThanOrEqualTo(3, result.RowCount, "Should have at least 3 devices");
    }

    [TestMethod]
    [Description("Hunt: find PowerShell with encoded commands")]
    public void Hunt_EncodedPowerShell()
    {
        var result = _runtime.Execute(
            """
            DeviceProcessEvents
            | where FileName == "powershell.exe"
            | where ProcessCommandLine contains "enc"
            | project Timestamp, DeviceName, AccountName, ProcessCommandLine
            """);
        AssertSuccess(result);
        Assert.AreEqual(1, result.RowCount);
    }

    [TestMethod]
    [Description("Hunt: find suspicious processes with extend")]
    public void Hunt_SuspiciousProcesses()
    {
        var result = _runtime.Execute(
            """
            DeviceProcessEvents
            | where FileName == "mimikatz.exe"
            | extend lower_cmd = tolower(ProcessCommandLine)
            | project Timestamp, DeviceName, AccountName, lower_cmd
            """);
        AssertSuccess(result);
        Assert.AreEqual(1, result.RowCount);
    }

    [TestMethod]
    [Description("Hunt: count events by file name, top 5")]
    public void Hunt_TopProcesses()
    {
        var result = _runtime.Execute(
            "DeviceProcessEvents | summarize count() by FileName | top 5 by count_ desc");
        AssertSuccess(result);
        Assert.IsTrue(result.RowCount >= 1 && result.RowCount <= 5);
    }

    [TestMethod]
    [Description("Hunt: recon command burst detection")]
    public void Hunt_ReconBurst()
    {
        var result = _runtime.Execute(
            """
            DeviceProcessEvents
            | where FileName in ("whoami.exe", "net.exe", "ipconfig.exe", "nltest.exe")
            | project Timestamp, DeviceName, FileName, ProcessCommandLine
            | sort by Timestamp asc
            """);
        AssertSuccess(result);
        Assert.AreEqual(4, result.RowCount, "Should find 4 recon commands");
    }

    // ─── Error cases ────────────────────────────────────────────────

    [TestMethod]
    [Description("Nonexistent table produces policy error")]
    public void Error_NonexistentTable()
    {
        var result = _runtime.Execute("FakeTable | take 10");
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.HasErrors);
    }

    [TestMethod]
    [Description("Empty KQL produces parse error")]
    public void Error_EmptyKql()
    {
        var result = _runtime.Execute("");
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.HasErrors);
    }

    [TestMethod]
    [Description("Broken KQL produces parse error")]
    public void Error_BrokenSyntax()
    {
        var result = _runtime.Execute("!!! not valid KQL !!!");
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.HasErrors);
    }

    [TestMethod]
    [Description("Bare join blocked with clear diagnostic")]
    public void Error_BareJoin()
    {
        var result = _runtime.Execute(
            "DeviceProcessEvents | join (DeviceProcessEvents | take 5) on DeviceName");
        Assert.IsFalse(result.Success);
        Assert.Contains(d =>
            d.Message.Contains("innerunique") || d.Message.Contains("kind"), result.Diagnostics.All);
    }

    // ─── Generated SQL available in developer mode ──────────────────

    [TestMethod]
    [Description("Successful query exposes generated SQL")]
    public void DeveloperMode_SqlAvailable()
    {
        var result = _runtime.Execute("DeviceProcessEvents | take 5");
        AssertSuccess(result);
        Assert.IsNotNull(result.GeneratedSql);
        Assert.Contains("main.DeviceProcessEvents", result.GeneratedSql);
    }

    [TestMethod]
    [Description("Failed query does not expose generated SQL")]
    public void DeveloperMode_NoSqlOnFailure()
    {
        var result = _runtime.Execute("FakeTable | take 5");
        Assert.IsFalse(result.Success);
        Assert.IsNull(result.GeneratedSql);
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private static void AssertSuccess(QueryResult result) => Assert.IsTrue(result.Success,
            $"Query failed:\n{string.Join("\n", result.Diagnostics.All.Select(d => d.ToString()))}");
}
