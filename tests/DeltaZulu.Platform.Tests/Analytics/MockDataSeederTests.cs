using System.Globalization;
using DeltaZulu.Platform.Data.Seeding;
using DuckDB.NET.Data;

namespace DeltaZulu.Platform.Tests.Analytics;

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

        Assert.HasCount(3, batches);

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
    public void GetMedallionSeedSql_IncludesRepresentativeDevelopmentScenarios()
    {
        var sql = MockDataSeeder.GetMedallionSeedSql();

        Assert.Contains("suspicious-powershell", sql);
        Assert.Contains("credential-access", sql);
        Assert.Contains("lateral-movement", sql);
        Assert.Contains("persistence-scheduled-task", sql);
        Assert.Contains("exfiltration", sql);
        Assert.Contains("4625", sql);
        Assert.Contains("4728", sql);
        Assert.Contains("dGhpcy1sb29rcy1saWtlLXR1bm5lbA.example.test", sql);
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
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }
}