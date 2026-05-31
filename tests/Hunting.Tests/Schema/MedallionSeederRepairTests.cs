namespace Hunting.Tests.Schema;

using Hunting.Core.DuckDbSql;
using Hunting.Data;
using Hunting.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class MedallionSeederRepairTests
{
    [TestMethod]
    public void MockDataSeeder_ExpectedRowCounts_MatchSeedTableSet()
    {
        var expectedCounts = MockDataSeeder.GetExpectedMedallionRowCountsByTable();
        var seedSqlByTable = MockDataSeeder.GetMedallionSeedSqlByTable();

        CollectionAssert.AreEquivalent(seedSqlByTable.Keys.ToArray(), expectedCounts.Keys.ToArray());

        foreach (var (table, expectedRows) in expectedCounts)
        {
            Assert.IsTrue(expectedRows > 0, $"{table} should have a positive expected development seed row count.");
        }
    }

    [TestMethod]
    public void MockDataSeeder_ExpectedRowCounts_MatchActualSeededRows()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateSchema(factory);

        foreach (var (table, seedSql) in MockDataSeeder.GetMedallionSeedSqlByTable())
        {
            applier.ExecuteRaw(seedSql);
            var actualRows = applier.QueryScalar($"SELECT count(*) FROM {table}");
            var expectedRows = MockDataSeeder.GetExpectedMedallionRowCountsByTable()[table];

            Assert.AreEqual(expectedRows, actualRows, $"{table} expected row count should match its seed SQL.");
        }
    }

    [TestMethod]
    public void MockDataSeeder_ExpectedRowCounts_CanDetectUnderseededTables()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateSchema(factory);

        applier.ExecuteRaw(
            """
            INSERT INTO bronze.dns_server_event (ingest_time, source_name, provider, host, raw_log, raw_text)
            VALUES (TIMESTAMP '2024-06-15 09:30:00', 'dns-server', 'Technitium DNS Server', 'DNS-001',
                    CAST('{"opcode":"QUERY","query_name":"thin.example.test"}' AS JSON), '')
            """);

        var existingRows = applier.QueryScalar("SELECT count(*) FROM bronze.dns_server_event");
        var expectedRows = MockDataSeeder.GetExpectedMedallionRowCountsByTable()["bronze.dns_server_event"];

        Assert.IsTrue(existingRows > 0);
        Assert.IsTrue(existingRows < expectedRows);
    }

    [TestMethod]
    public void MockDataSeeder_RepairPattern_CanReplaceUnderseededTable()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateSchema(factory);

        applier.ExecuteRaw(
            """
            INSERT INTO bronze.dns_server_event (ingest_time, source_name, provider, host, raw_log, raw_text)
            VALUES (TIMESTAMP '2024-06-15 09:30:00', 'dns-server', 'Technitium DNS Server', 'DNS-001',
                    CAST('{"opcode":"QUERY","query_name":"thin.example.test"}' AS JSON), '')
            """);

        var table = "bronze.dns_server_event";
        var expectedRows = MockDataSeeder.GetExpectedMedallionRowCountsByTable()[table];
        var existingRows = applier.QueryScalar($"SELECT count(*) FROM {table}");

        if (existingRows < expectedRows)
        {
            applier.ExecuteRaw($"DELETE FROM {table}");
            applier.ExecuteRaw(MockDataSeeder.GetMedallionSeedSqlByTable()[table]);
        }

        Assert.AreEqual(expectedRows, applier.QueryScalar($"SELECT count(*) FROM {table}"));
        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.Dns WHERE QueryName = 'blocked.example.test'") >= 1);
    }

    private static SchemaApplier CreateSchema(DuckDbConnectionFactory factory)
    {
        var applier = new SchemaApplier(factory);
        var ddl = new SchemaEmitter().EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: [],
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        applier.ApplyStatements(ddl);
        return applier;
    }
}
