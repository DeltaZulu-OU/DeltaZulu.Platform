using DeltaZulu.Platform.Data.DuckDb;
using DeltaZulu.Platform.Data.DuckDb.Sql;
using DeltaZulu.Platform.Domain.Analytics.Schema;
using DeltaZulu.Platform.Tests.Analytics.Fixtures;

namespace DeltaZulu.Platform.Tests.Analytics.Spike;

/// <summary>
/// Integration tests for the active Phase 1A medallion schema pipeline:
/// C# models → SchemaEmitter → DDL → SchemaApplier → DuckDB → seeded Golden views.
/// </summary>
[TestClass]
public sealed class SchemaPipelineTests
{
    private static SchemaApplier _applier = null!;
    private static SchemaEmitter _emitter = null!;
    private static DuckDbConnectionFactory _factory = null!;

    [ClassCleanup]
    public static void Cleanup() => _factory.Dispose();

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        _factory = new DuckDbConnectionFactory("DataSource=:memory:");
        _applier = new SchemaApplier(_factory);
        _emitter = new SchemaEmitter();

        var ddl = _emitter.EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: SchemaConventions.InternalTables,
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        _applier.ApplyStatements(ddl);
        _applier.ExecuteRaw(MedallionTestData.GetMedallionSeedSql());
    }

    [TestMethod]
    public void EmitAll_ProducesActiveMedallionDdl()
    {
        var ddl = _emitter.EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: SchemaConventions.InternalTables,
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        Assert.IsGreaterThanOrEqualTo(15, ddl.Count, $"Expected active medallion DDL, got {ddl.Count} statements.");
        Assert.Contains(sql => sql.Contains("CREATE TABLE IF NOT EXISTS bronze.windows_sysmon_event"), ddl);
        Assert.Contains(sql => sql.Contains("CREATE TABLE IF NOT EXISTS bronze.windows_security_event"), ddl);
        Assert.Contains(sql => sql.Contains("CREATE TABLE IF NOT EXISTS bronze.dns_server_event"), ddl);
        Assert.Contains(sql => sql.Contains("CREATE OR REPLACE VIEW golden.ProcessEvent"), ddl);
        Assert.Contains(sql => sql.Contains("CREATE OR REPLACE VIEW golden.NetworkSession"), ddl);
        Assert.Contains(sql => sql.Contains("CREATE OR REPLACE VIEW golden.Dns"), ddl);
    }

    [TestMethod]
    public void EmitCanonicalViews_ReferenceActiveSilverViews()
    {
        foreach (var view in SchemaConventions.CanonicalViews)
        {
            var sql = _emitter.EmitCanonicalView(view);

            Assert.Contains($"CREATE OR REPLACE VIEW {view.QualifiedName}", sql);
            foreach (var parserView in view.ParserViews)
            {
                Assert.Contains($"FROM {parserView}", sql);
            }

            Assert.DoesNotContain("ProcessEvents", sql);
            Assert.DoesNotContain("NetworkSessions", sql);
        }
    }

    [TestMethod]
    public void EmitParserViews_ReferenceActiveBronzeSources()
    {
        var parserSql = SchemaConventions.ParserViews
            .Select(view => _emitter.EmitParserView(view))
            .ToArray();

        Assert.Contains(sql => sql.Contains("FROM bronze.windows_sysmon_event"), parserSql);
        Assert.Contains(sql => sql.Contains("FROM bronze.windows_security_event"), parserSql);
        Assert.Contains(sql => sql.Contains("FROM bronze.dns_server_event"), parserSql);
        Assert.DoesNotContain(sql => sql.Contains("windows_event_json"), parserSql);
    }

    [TestMethod]
    public void EmitRawTables_UseActiveBronzeSourceEnvelope()
    {
        foreach (var table in SchemaConventions.RawTables)
        {
            var sql = _emitter.EmitCreateTable(table);

            Assert.Contains("ingest_time", sql);
            Assert.Contains("source_name", sql);
            Assert.Contains("provider", sql);
            Assert.Contains("host", sql);
            Assert.Contains("raw_log", sql);
            Assert.Contains("raw_text", sql);
            Assert.DoesNotContain("windows_event_json", sql);
            Assert.DoesNotContain("event_data", sql);
            Assert.DoesNotContain("source_type", sql);
        }
    }

    [TestMethod]
    public void Extraction_ProcessEventRepresentativeFields()
    {
        Assert.IsGreaterThanOrEqualTo(1, _applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent WHERE FileName = 'cmd.exe'"));
        Assert.IsGreaterThanOrEqualTo(5, _applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent WHERE FolderPath LIKE '%System32%'"));
        Assert.IsGreaterThanOrEqualTo(5, _applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent WHERE AccountName = 'CORP\\alice'"));
        Assert.IsGreaterThanOrEqualTo(1, _applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent WHERE ProcessCommandLine LIKE '%mimikatz%'"));
        Assert.AreEqual(1L, _applier.QueryScalar("SELECT count(DISTINCT ActionType) FROM golden.ProcessEvent"));
    }

    [TestMethod]
    public void Analytics_NetworkAndDnsScenarios()
    {
        Assert.IsGreaterThanOrEqualTo(1, _applier.QueryScalar("SELECT count(*) FROM golden.NetworkSession WHERE RemotePort = 4444"));
        Assert.IsGreaterThanOrEqualTo(1, _applier.QueryScalar("SELECT count(*) FROM golden.NetworkSession WHERE RemotePort = 445"));
        Assert.IsGreaterThanOrEqualTo(4, _applier.QueryScalar("SELECT count(*) FROM golden.NetworkSession WHERE RemoteIP = '203.0.113.60' AND RemoteUrl IS NULL"));
        Assert.IsGreaterThanOrEqualTo(3, _applier.QueryScalar("SELECT count(*) FROM golden.Dns WHERE QueryName LIKE '%example.test%'"));
    }

    [TestMethod]
    public void Analytics_ProcessScenarios()
    {
        Assert.IsGreaterThanOrEqualTo(3, _applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent WHERE FileName IN ('mimikatz.exe', 'rundll32.exe', 'procdump.exe')"));
        Assert.IsGreaterThanOrEqualTo(4, _applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent WHERE FileName IN ('whoami.exe', 'net.exe', 'ipconfig.exe', 'nltest.exe')"));
        Assert.IsGreaterThanOrEqualTo(4, _applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent WHERE FileName IN ('schtasks.exe', 'reg.exe', 'sc.exe', 'at.exe')"));
        Assert.IsGreaterThanOrEqualTo(2, _applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent WHERE FileName = 'powershell.exe' AND lower(ProcessCommandLine) LIKE '%enc%'"));
    }

    [TestMethod]
    public void MockData_FlowsIntoActiveGoldenViews()
    {
        Assert.IsGreaterThanOrEqualTo(10, _applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent"));
        Assert.IsGreaterThanOrEqualTo(4, _applier.QueryScalar("SELECT count(*) FROM golden.NetworkSession"));
        Assert.IsGreaterThanOrEqualTo(5, _applier.QueryScalar("SELECT count(*) FROM golden.Dns"));
    }

    [TestMethod]
    public void MockData_InsertedIntoActiveBronzeTables()
    {
        var expected = MedallionTestData.ExpectedRowsByTable;

        foreach (var (table, expectedRows) in expected)
        {
            var count = _applier.QueryScalar($"SELECT count(*) FROM {table}");
            Assert.AreEqual(expectedRows, count, $"{table} should contain its expected test fixture rows.");
        }
    }

    [TestMethod]
    public void Validate_ActiveBronzeTables()
    {
        foreach (var table in SchemaConventions.RawTables)
        {
            var mismatches = _applier.Validate(table);
            Assert.IsEmpty(mismatches, $"{table.QualifiedName} mismatches:\n{string.Join("\n", mismatches.Select(m => m.Message))}");
        }
    }

    [TestMethod]
    public void Validate_ActiveGoldenViews()
    {
        foreach (var view in SchemaConventions.CanonicalViews)
        {
            var mismatches = _applier.Validate(view);
            var missing = mismatches.Where(m => m.ActualType == "MISSING").ToList();

            Assert.IsEmpty(missing, $"{view.QualifiedName} missing columns:\n{string.Join("\n", missing.Select(m => m.Message))}");
        }
    }
}
