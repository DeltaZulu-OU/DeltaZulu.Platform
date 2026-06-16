using DeltaZulu.Platform.Application.Analytics.Planning;
using DeltaZulu.Platform.Data.Analytics;
using DeltaZulu.Platform.Data.DuckDb;
using DeltaZulu.Platform.Data.DuckDb.Sql;
using DeltaZulu.Platform.Domain.Analytics.Catalog;
using DeltaZulu.Platform.Domain.Analytics.QueryModel;
using DeltaZulu.Platform.Domain.Analytics.Schema;
using DeltaZulu.Platform.Tests.Analytics.Fixtures;

namespace DeltaZulu.Platform.Tests.Analytics.Translation;

[TestClass]
public sealed class EndToEndPipelineTests
{
    private static DuckDbConnectionFactory _factory = null!;
    private static QueryRuntime _runtime = null!;

    [ClassCleanup]
    public static void Cleanup() => _factory.Dispose();

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        _factory = new DuckDbConnectionFactory("DataSource=:memory:");

        var emitter = new SchemaEmitter();
        var applier = new SchemaApplier(_factory);
        var ddl = emitter.EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: [],
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        applier.ApplyStatements(ddl);
        applier.ExecuteRaw(MedallionTestData.GetMedallionSeedSql());

        _runtime = new QueryRuntime(CreateMedallionCatalog(), _factory, defaultLimit: 10_000, developerMode: true);
    }

    [TestMethod]
    public void DeveloperMode_NoSqlOnFailure()
    {
        var result = _runtime.Execute("FakeTable | take 5");

        Assert.IsFalse(result.Success);
        Assert.IsNull(result.GeneratedSql);
        Assert.IsNull(result.PlannerStatsJson);
        Assert.IsNotEmpty(result.DebugTrace);
    }

    [TestMethod]
    public void DeveloperMode_SqlAvailable()
    {
        var result = _runtime.Execute("ProcessEvent | take 5");

        AssertSuccess(result);
        Assert.IsNotNull(result.GeneratedSql);
        Assert.Contains("golden.ProcessEvent", result.GeneratedSql);
        Assert.IsNotEmpty(result.DebugTrace);
    }

    [TestMethod]
    public void Error_BareJoin()
    {
        var result = _runtime.Execute("ProcessEvent | join (ProcessEvent | take 5) on DeviceName");

        Assert.IsFalse(result.Success);
        Assert.Contains(d => d.Message.Contains("innerunique") || d.Message.Contains("kind"), result.Diagnostics.All);
    }

    [TestMethod]
    public void Error_BrokenSyntax()
    {
        var result = _runtime.Execute("!!! not valid KQL !!!");

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.HasErrors);
    }

    [TestMethod]
    public void Error_EmptyKql()
    {
        var result = _runtime.Execute("");

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.HasErrors);
    }

    [TestMethod]
    public void Error_NonexistentTable()
    {
        var result = _runtime.Execute("FakeTable | take 10");

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.HasErrors);
    }

    [TestMethod]
    public void Hunt_AllProcessEventRows()
    {
        var result = _runtime.Execute("ProcessEvent | take 100");

        AssertSuccess(result);
        Assert.IsGreaterThanOrEqualTo(10, result.RowCount, $"Expected at least 10 ProcessEvent rows, got {result.RowCount}.");
    }

    [TestMethod]
    public void Hunt_CountProcessEventRowsByDevice()
    {
        var result = _runtime.Execute("ProcessEvent | summarize count() by DeviceName | sort by count_ desc");

        AssertSuccess(result);
        Assert.IsGreaterThanOrEqualTo(3, result.RowCount, $"Expected at least 3 devices, got {result.RowCount}.");
    }

    [TestMethod]
    public void Hunt_DnsTestDomains()
    {
        var result = _runtime.Execute(
            """
            Dns
            | where QueryName has "example.test"
            | project Timestamp, DeviceName, QueryName, ResponseCode, ResponseIP, SrcIpAddr
            """);

        AssertSuccess(result);
        Assert.IsGreaterThanOrEqualTo(1, result.RowCount, $"Expected DNS example.test rows, got {result.RowCount}.");
    }

    [TestMethod]
    public void Hunt_EncodedPowerShell()
    {
        var result = _runtime.Execute(
            """
            ProcessEvent
            | where FileName == "powershell.exe"
            | where ProcessCommandLine contains "enc" or ProcessCommandLine contains "EncodedCommand"
            | project Timestamp, DeviceName, AccountName, ProcessCommandLine
            """);

        AssertSuccess(result);
        Assert.IsGreaterThanOrEqualTo(1, result.RowCount, $"Expected encoded PowerShell rows, got {result.RowCount}.");
    }

    [TestMethod]
    public void Hunt_ReconCommands()
    {
        var result = _runtime.Execute(
            """
            ProcessEvent
            | where FileName in ("whoami.exe", "net.exe", "ipconfig.exe", "nltest.exe")
            | project Timestamp, DeviceName, FileName, ProcessCommandLine
            | sort by Timestamp asc
            """);

        AssertSuccess(result);
        Assert.IsGreaterThanOrEqualTo(1, result.RowCount, $"Expected recon command rows, got {result.RowCount}.");
    }

    [TestMethod]
    public void Hunt_SuspiciousNetworkPorts()
    {
        var result = _runtime.Execute(
            """
            NetworkSession
            | where RemotePort in (4444, 1337, 8888, 9999, 31337)
            | project Timestamp, DeviceName, LocalIP, RemoteIP, RemotePort, InitiatingProcessFileName
            """);

        AssertSuccess(result);
        Assert.IsGreaterThanOrEqualTo(1, result.RowCount, $"Expected suspicious NetworkSession rows, got {result.RowCount}.");
    }

    [TestMethod]
    public void Hunt_SuspiciousProcesses()
    {
        var result = _runtime.Execute(
            """
            ProcessEvent
            | where FileName == "mimikatz.exe"
            | extend lower_cmd = tolower(ProcessCommandLine)
            | project Timestamp, DeviceName, AccountName, lower_cmd
            """);

        AssertSuccess(result);
        Assert.IsGreaterThanOrEqualTo(1, result.RowCount, $"Expected mimikatz.exe rows, got {result.RowCount}.");
    }

    [TestMethod]
    public void Hunt_TopProcesses()
    {
        var result = _runtime.Execute("ProcessEvent | summarize count() by FileName | top 5 by count_ desc");

        AssertSuccess(result);
        Assert.IsTrue(result.RowCount >= 1 && result.RowCount <= 5);
    }

    [TestMethod]
    public void Planner_DefaultPlanner_PreservesSemantics()
    {
        var runtimeOff = new QueryRuntime(CreateMedallionCatalog(), _factory, defaultLimit: 10_000, developerMode: true, planner: new NoOpRelationalPlanner());
        var runtimeOn = new QueryRuntime(CreateMedallionCatalog(), _factory, defaultLimit: 10_000, developerMode: true);

        const string kql = "ProcessEvent | where FileName == \"cmd.exe\" | project Timestamp, DeviceName | take 5";

        var off = runtimeOff.Execute(kql);
        var on = runtimeOn.Execute(kql);

        AssertSuccess(off);
        AssertSuccess(on);
        Assert.AreEqual(off.RowCount, on.RowCount);
        Assert.AreEqual(off.ColumnCount, on.ColumnCount);
    }

    [TestMethod]
    public void Planner_Exception_ProducesDiagnostic()
    {
        var runtime = new QueryRuntime(
            CreateMedallionCatalog(),
            _factory,
            defaultLimit: 10_000,
            developerMode: true,
            planner: new ThrowingPlanner());

        var result = runtime.Execute("ProcessEvent | take 1");

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.HasErrors);
        Assert.Contains(e => e.Message.Contains("logical planning stage", StringComparison.OrdinalIgnoreCase), result.Diagnostics.Errors);
    }

    [TestMethod]
    public void Planner_MaxIterations_IsForwarded()
    {
        var capturing = new CapturingPlanner();
        var runtime = new QueryRuntime(
            CreateMedallionCatalog(),
            _factory,
            defaultLimit: 10_000,
            developerMode: true,
            plannerMaxIterations: 7,
            planner: capturing);

        var result = runtime.Execute("ProcessEvent | take 1");

        AssertSuccess(result);
        Assert.AreEqual(7, capturing.LastContext?.MaxIterations);
        Assert.IsTrue(capturing.LastContext?.Enabled);
    }

    [TestMethod]
    public void Planner_NoOp_PreservesBehavior()
    {
        var runtimeDefault = new QueryRuntime(CreateMedallionCatalog(), _factory, defaultLimit: 10_000, developerMode: true);
        var runtimeNoOp = new QueryRuntime(CreateMedallionCatalog(), _factory, defaultLimit: 10_000, developerMode: true, planner: new NoOpRelationalPlanner());

        const string kql = "ProcessEvent | where FileName == \"cmd.exe\" | project Timestamp, DeviceName | take 5";

        var off = runtimeDefault.Execute(kql);
        var on = runtimeNoOp.Execute(kql);

        AssertSuccess(off);
        AssertSuccess(on);
        Assert.AreEqual(off.RowCount, on.RowCount);
        Assert.AreEqual(off.ColumnCount, on.ColumnCount);
        Assert.AreEqual(off.GeneratedSql, on.GeneratedSql);
        Assert.IsNotNull(off.PlannerStatsJson);
    }

    [TestMethod]
    public void PlannerGateway_BypassesSimpleShape()
    {
        var capturing = new CapturingPlanner();
        var runtime = new QueryRuntime(
            CreateMedallionCatalog(),
            _factory,
            defaultLimit: 10_000,
            developerMode: true,
            planner: capturing,
            plannerGatewayEnabled: true);

        var result = runtime.Execute("ProcessEvent | project Timestamp, DeviceName | take 1");

        AssertSuccess(result);
        Assert.IsNull(capturing.LastContext);
        Assert.Contains(d => d.Contains("Planner gateway: decision=bypass", StringComparison.OrdinalIgnoreCase), result.DebugTrace);
    }

    [TestMethod]
    public void PlannerGateway_RunsPlanner_WhenAboveThreshold()
    {
        var capturing = new CapturingPlanner();
        var runtime = new QueryRuntime(
            CreateMedallionCatalog(),
            _factory,
            defaultLimit: 10_000,
            developerMode: true,
            planner: capturing,
            plannerGatewayEnabled: true,
            plannerGatewayMaxEstimatedRows: 0);

        var result = runtime.Execute("ProcessEvent | summarize count() by DeviceName");

        AssertSuccess(result);
        Assert.IsNotNull(capturing.LastContext);
        Assert.Contains(d => d.Contains("Planner gateway: decision=run", StringComparison.OrdinalIgnoreCase), result.DebugTrace);
    }

    [TestMethod]
    public void Runtime_ExecuteStreamed_EarlyStop()
    {
        var seen = 0;
        var result = _runtime.ExecuteStreamed(
            "ProcessEvent | take 100",
            _ => {
                seen++;
                return seen < 3;
            });

        Assert.IsTrue(result.Success);
        Assert.AreEqual(3, seen);
        Assert.AreEqual(3, result.RowCount);
    }

    [TestMethod]
    public void Runtime_ExecuteStreamed_Success()
    {
        var streamedRows = new List<object?[]>();
        var result = _runtime.ExecuteStreamed(
            "ProcessEvent | project DeviceName, FileName | take 5",
            reader => {
                var row = new object?[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[i] = DuckDbValueReader.ReadValue(reader, i);
                }

                streamedRows.Add(row);
                return true;
            });

        Assert.IsTrue(result.Success);
        Assert.HasCount(result.RowCount, streamedRows);
        Assert.HasCount(2, result.Columns);
        Assert.AreEqual("DeviceName", result.Columns[0].Name);
        Assert.AreEqual("FileName", result.Columns[1].Name);
        Assert.IsNotNull(result.SqlShapeStatsJson);
    }

    [TestMethod]
    public void RuntimeCtor_InvalidParameters_Throw()
    {
        var catalog = CreateMedallionCatalog();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QueryRuntime(catalog, _factory, defaultLimit: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QueryRuntime(catalog, _factory, timeoutSeconds: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QueryRuntime(catalog, _factory, plannerMaxIterations: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QueryRuntime(catalog, _factory, plannerGatewayMaxEstimatedRows: -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QueryRuntime(catalog, _factory, plannerGatewayJoinComplexityThreshold: -1));
    }

    [TestMethod]
    public void SqlShapeStats_AvailableOnSuccess()
    {
        var result = _runtime.Execute("ProcessEvent | where FileName == \"cmd.exe\" | take 5");

        AssertSuccess(result);
        Assert.IsNotNull(result.SqlShapeStatsJson);

        var stats = System.Text.Json.JsonSerializer.Deserialize<SqlShapeMetrics>(result.SqlShapeStatsJson!);
        Assert.IsNotNull(stats);
        Assert.IsGreaterThanOrEqualTo(1, stats.SelectCount);
        Assert.IsGreaterThan(0, stats.SqlLength);
    }

    [TestMethod]
    public void SqlShapeStats_AvailableWhenDeveloperModeDisabled()
    {
        var runtime = new QueryRuntime(CreateMedallionCatalog(), _factory, defaultLimit: 10_000, developerMode: false);
        var result = runtime.Execute("ProcessEvent | take 3");

        AssertSuccess(result);
        Assert.IsNull(result.GeneratedSql);
        Assert.IsNotNull(result.SqlShapeStatsJson);
    }

    [TestMethod]
    public void VerticalSlice()
    {
        var result = _runtime.Execute(
            """
            ProcessEvent
            | where FileName == "cmd.exe"
            | project Timestamp, DeviceName, ProcessCommandLine
            | take 20
            """);

        AssertSuccess(result);
        Assert.IsGreaterThanOrEqualTo(1, result.RowCount, $"Expected at least one cmd.exe row, got {result.RowCount}.");
        Assert.AreEqual(3, result.ColumnCount);
        Assert.AreEqual("Timestamp", result.Columns[0].Name);
        Assert.AreEqual("DeviceName", result.Columns[1].Name);
        Assert.AreEqual("ProcessCommandLine", result.Columns[2].Name);
    }

    private static void AssertSuccess(QueryResult result)
    {
        if (!result.Success)
        {
            var errors = string.Join(Environment.NewLine, result.Diagnostics.Errors.Select(e => e.Message));
            Assert.Fail($"Expected query to succeed, but it failed:{Environment.NewLine}{errors}");
        }
    }

    private static ApprovedViewCatalog CreateMedallionCatalog()
    {
        var catalog = new ApprovedViewCatalog();
        catalog.RegisterAll(SchemaConventions.CanonicalViews);
        return catalog;
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

    private sealed class ThrowingPlanner : IRelationalPlanner
    {
        public RelNode Plan(RelNode root, PlannerContext context) =>
            throw new InvalidOperationException("boom");
    }
}