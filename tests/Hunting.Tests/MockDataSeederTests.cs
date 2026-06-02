namespace Hunting.Tests;

using DuckDB.NET.Data;
using Hunting.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class MockDataSeederTests
{
    private const string WindowsSysmonTable = "bronze.windows_sysmon_event";
    private const string WindowsSecurityTable = "bronze.windows_security_event";
    private const string DnsServerTable = "bronze.dns_server_event";

    [TestMethod]
    public void GetExpectedMedallionRowCountsByTable_ReturnsExpandedDevelopmentSeedCounts()
    {
        var counts = MockDataSeeder.GetExpectedMedallionRowCountsByTable();

        Assert.AreEqual(320L, counts[WindowsSysmonTable]);
        Assert.AreEqual(100L, counts[WindowsSecurityTable]);
        Assert.AreEqual(80L, counts[DnsServerTable]);
        Assert.AreEqual(500L, counts.Values.Sum());
    }

    [TestMethod]
    public void GetMedallionSeedFixtureBatches_ReturnsOneGovernedBatchPerBronzeSource()
    {
        var batches = MockDataSeeder.GetMedallionSeedFixtureBatches("test-catalog");

        Assert.AreEqual(3, batches.Count);

        var byTable = batches.ToDictionary(batch => batch.TableName, StringComparer.OrdinalIgnoreCase);
        Assert.AreEqual(320L, byTable[WindowsSysmonTable].RowCount);
        Assert.AreEqual(100L, byTable[WindowsSecurityTable].RowCount);
        Assert.AreEqual(80L, byTable[DnsServerTable].RowCount);

        foreach (var batch in batches)
        {
            Assert.AreEqual("development.baseline", batch.Scenario);
            Assert.AreEqual("test-catalog", batch.CatalogVersion);
            Assert.IsFalse(string.IsNullOrWhiteSpace(batch.BatchId));
            Assert.IsFalse(string.IsNullOrWhiteSpace(batch.ContentHash));
            Assert.AreEqual(64, batch.ContentHash.Length);
        }
    }

    [TestMethod]
    public void GetMedallionSeedSqlByTable_EmitsDeclaredDevelopmentRowCounts()
    {
        var sqlByTable = MockDataSeeder.GetMedallionSeedSqlByTable();

        Assert.AreEqual(320, CountInsertRows(sqlByTable[WindowsSysmonTable]));
        Assert.AreEqual(100, CountInsertRows(sqlByTable[WindowsSecurityTable]));
        Assert.AreEqual(80, CountInsertRows(sqlByTable[DnsServerTable]));
    }

    [TestMethod]
    public void GetMedallionSeedSqlByTable_ExecutesAgainstBronzeTableContracts()
    {
        using var connection = new DuckDBConnection("Data Source=:memory:");
        connection.Open();

        ExecuteNonQuery(connection, "CREATE SCHEMA bronze;");
        CreateBronzeSeedTable(connection, WindowsSysmonTable);
        CreateBronzeSeedTable(connection, WindowsSecurityTable);
        CreateBronzeSeedTable(connection, DnsServerTable);

        foreach (var sql in MockDataSeeder.GetMedallionSeedSqlByTable().Values)
        {
            ExecuteNonQuery(connection, sql);
        }

        Assert.AreEqual(320L, QueryCount(connection, WindowsSysmonTable));
        Assert.AreEqual(100L, QueryCount(connection, WindowsSecurityTable));
        Assert.AreEqual(80L, QueryCount(connection, DnsServerTable));
    }

    [TestMethod]
    public void GetMedallionSeedSql_IncludesRepresentativeDevelopmentHuntingScenarios()
    {
        var sql = MockDataSeeder.GetMedallionSeedSql();

        StringAssert.Contains(sql, "suspicious-powershell");
        StringAssert.Contains(sql, "credential-access");
        StringAssert.Contains(sql, "lateral-movement");
        StringAssert.Contains(sql, "persistence-scheduled-task");
        StringAssert.Contains(sql, "exfiltration");
        StringAssert.Contains(sql, "4625");
        StringAssert.Contains(sql, "4728");
        StringAssert.Contains(sql, "dGhpcy1sb29rcy1saWtlLXR1bm5lbA.example.test");
    }

    private static int CountInsertRows(string sql) =>
        sql.Split("(TIMESTAMP '", StringSplitOptions.None).Length - 1;

    private static void CreateBronzeSeedTable(DuckDBConnection connection, string tableName) =>
        ExecuteNonQuery(
            connection,
            $"""
            CREATE TABLE {tableName} (
                ingest_time TIMESTAMP,
                source_name VARCHAR,
                provider VARCHAR,
                host VARCHAR,
                raw_log JSON,
                raw_text VARCHAR
            );
            """);

    private static void ExecuteNonQuery(DuckDBConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static long QueryCount(DuckDBConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT count(*) FROM {tableName};";
        return Convert.ToInt64(command.ExecuteScalar());
    }
}
