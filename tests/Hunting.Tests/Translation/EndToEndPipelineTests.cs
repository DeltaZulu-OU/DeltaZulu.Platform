namespace Hunting.Tests.Translation;

using Hunting.Core.Catalog;
using Hunting.Core.DuckDbSql;
using Hunting.Core.Planning;
using Hunting.Core.QueryModel;
using Hunting.Data;
using Hunting.Schema.Definitions;

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

    [TestMethod]
    [Description("Planner flag on with no-op planner preserves generated SQL and results")]
    public void Planner_NoOp_PreservesBehavior()
    {
        var catalog = new ApprovedViewCatalog();
        catalog.Register(DeviceProcessEventsSchema.View);

        var runtimeOff = new QueryRuntime(catalog, _factory, defaultLimit: 10_000, developerMode: true, plannerEnabled: false);
        var runtimeOn = new QueryRuntime(catalog, _factory, defaultLimit: 10_000, developerMode: true, plannerEnabled: true, planner: new NoOpRelationalPlanner());

        const string kql = "DeviceProcessEvents | where FileName == \"cmd.exe\" | project Timestamp, DeviceName | take 5";

        var off = runtimeOff.Execute(kql);
        var on = runtimeOn.Execute(kql);

        AssertSuccess(off);
        AssertSuccess(on);
        Assert.AreEqual(off.RowCount, on.RowCount);
        Assert.AreEqual(off.ColumnCount, on.ColumnCount);
        Assert.AreEqual(off.GeneratedSql, on.GeneratedSql, "No-op planner should preserve emitted SQL");
        Assert.IsNull(off.PlannerStatsJson, "Planner stats should be hidden when planner is disabled");
        Assert.IsNotNull(on.PlannerStatsJson, "Planner stats should be present in developer mode when planner runs");
    }

    [TestMethod]
    [Description("Planner flag on with default planner preserves query semantics")]
    public void Planner_DefaultPlanner_PreservesSemantics()
    {
        var catalog = new ApprovedViewCatalog();
        catalog.Register(DeviceProcessEventsSchema.View);

        var runtimeOff = new QueryRuntime(catalog, _factory, defaultLimit: 10_000, developerMode: true, plannerEnabled: false);
        var runtimeOn = new QueryRuntime(catalog, _factory, defaultLimit: 10_000, developerMode: true, plannerEnabled: true);

        const string kql = "DeviceProcessEvents | where FileName == \"cmd.exe\" | project Timestamp, DeviceName | take 5";

        var off = runtimeOff.Execute(kql);
        var on = runtimeOn.Execute(kql);

        AssertSuccess(off);
        AssertSuccess(on);
        Assert.AreEqual(off.RowCount, on.RowCount);
        Assert.AreEqual(off.ColumnCount, on.ColumnCount);

        for (var i = 0; i < off.RowCount; i++)
        {
            CollectionAssert.AreEqual(off.Rows[i], on.Rows[i]);
        }
    }

    [TestMethod]
    [Description("Planner exceptions are captured as diagnostics")]
    public void Planner_Exception_ProducesDiagnostic()
    {
        var catalog = new ApprovedViewCatalog();
        catalog.Register(DeviceProcessEventsSchema.View);

        var runtime = new QueryRuntime(
            catalog,
            _factory,
            defaultLimit: 10_000,
            developerMode: true,
            plannerEnabled: true,
            planner: new ThrowingPlanner());

        var result = runtime.Execute("DeviceProcessEvents | take 1");

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.HasErrors);
        Assert.Contains(e => e.Message.Contains("logical planning stage", StringComparison.OrdinalIgnoreCase), result.Diagnostics.Errors);
    }

    [TestMethod]
    [Description("Planner max iterations is forwarded to planner context")]
    public void Planner_MaxIterations_IsForwarded()
    {
        var catalog = new ApprovedViewCatalog();
        catalog.Register(DeviceProcessEventsSchema.View);

        var capturing = new CapturingPlanner();
        var runtime = new QueryRuntime(
            catalog,
            _factory,
            defaultLimit: 10_000,
            developerMode: true,
            plannerEnabled: true,
            plannerMaxIterations: 7,
            planner: capturing);

        var result = runtime.Execute("DeviceProcessEvents | take 1");

        AssertSuccess(result);
        Assert.AreEqual(7, capturing.LastContext?.MaxIterations);
        Assert.IsTrue(capturing.LastContext?.Enabled);
    }

    [TestMethod]
    [Description("Runtime constructor rejects invalid guardrail parameters")]
    public void RuntimeCtor_InvalidParameters_Throw()
    {
        var catalog = new ApprovedViewCatalog();
        catalog.Register(DeviceProcessEventsSchema.View);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QueryRuntime(catalog, _factory, defaultLimit: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QueryRuntime(catalog, _factory, timeoutSeconds: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QueryRuntime(catalog, _factory, plannerMaxIterations: 0));
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

    [TestMethod]
    [Description("Successful query always exposes SqlShapeStatsJson")]
    public void SqlShapeStats_AvailableOnSuccess()
    {
        var result = _runtime.Execute("DeviceProcessEvents | where FileName == \"cmd.exe\" | take 5");
        AssertSuccess(result);
        Assert.IsNotNull(result.SqlShapeStatsJson);

        var stats = System.Text.Json.JsonSerializer.Deserialize<SqlShapeMetrics>(result.SqlShapeStatsJson!);
        Assert.IsNotNull(stats);
        Assert.IsGreaterThanOrEqualTo(1, stats.SelectCount);
        Assert.IsGreaterThanOrEqualTo(0, stats.CteStageCount);
        Assert.IsGreaterThanOrEqualTo(0, stats.JoinCount);
        Assert.IsGreaterThanOrEqualTo(0, stats.OrderByCount);
        Assert.IsGreaterThanOrEqualTo(0, stats.LimitCount);
        Assert.IsGreaterThan(0, stats.SqlLength);
    }

    [TestMethod]
    [Description("SqlShapeStatsJson is available even when developer mode is disabled")]
    public void SqlShapeStats_AvailableWhenDeveloperModeDisabled()
    {
        var catalog = new ApprovedViewCatalog();
        catalog.Register(DeviceProcessEventsSchema.View);

        var runtime = new QueryRuntime(catalog, _factory, defaultLimit: 10_000, developerMode: false);
        var result = runtime.Execute("DeviceProcessEvents | take 3");

        AssertSuccess(result);
        Assert.IsNull(result.GeneratedSql, "Generated SQL should remain hidden outside developer mode");
        Assert.IsNotNull(result.SqlShapeStatsJson, "SQL-shape stats should be available regardless of developer mode");
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
        Assert.IsNotEmpty(result.DebugTrace, "Developer mode should include debug trace events");
    }

    [TestMethod]
    [Description("Failed query does not expose generated SQL")]
    public void DeveloperMode_NoSqlOnFailure()
    {
        var result = _runtime.Execute("FakeTable | take 5");
        Assert.IsFalse(result.Success);
        Assert.IsNull(result.GeneratedSql);
        Assert.IsNull(result.PlannerStatsJson);
        Assert.IsNotEmpty(result.DebugTrace, "Failure in developer mode should still include debug trace");
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private static void AssertSuccess(QueryResult result) => Assert.IsTrue(result.Success,
            $"Query failed:\n{string.Join("\n", result.Diagnostics.All.Select(d => d.ToString()))}");

    private sealed class ThrowingPlanner : IRelationalPlanner
    {
        public RelNode Plan(RelNode root, PlannerContext context) => throw new InvalidOperationException("planner boom");
    }

    private sealed class CapturingPlanner : IRelationalPlanner
    {
        public PlannerContext? LastContext { get; private set; }

        public RelNode Plan(RelNode root, PlannerContext context)
        {
            LastContext = context;
            return root;
        }
    }
}