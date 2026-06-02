namespace Hunting.Tests.Schema;

using Hunting.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class Phase1CSeedFixtureBatchTests
{
    [TestMethod]
    public void SeedFixtureBatch_TrimsRequiredFieldsAndComputesContentHash()
    {
        var batch = new SeedFixtureBatch(
            BatchId: " batch ",
            TableName: " bronze.windows_sysmon_event ",
            SourceName: " sysmon ",
            Scenario: " process.basic ",
            Sql: " INSERT INTO bronze.windows_sysmon_event VALUES (1); ",
            RowCount: 1,
            CatalogVersion: " phase-1c ");

        Assert.AreEqual("batch", batch.BatchId);
        Assert.AreEqual("bronze.windows_sysmon_event", batch.TableName);
        Assert.AreEqual("sysmon", batch.SourceName);
        Assert.AreEqual("process.basic", batch.Scenario);
        Assert.AreEqual("INSERT INTO bronze.windows_sysmon_event VALUES (1);", batch.Sql);
        Assert.AreEqual("phase-1c", batch.CatalogVersion);
        Assert.AreEqual(64, batch.ContentHash.Length);
    }

    [TestMethod]
    public void SeedFixtureBatch_RejectsNegativeRowCount() => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                                                                               new SeedFixtureBatch(
                                                                                   BatchId: "batch",
                                                                                   TableName: "bronze.windows_sysmon_event",
                                                                                   SourceName: "sysmon",
                                                                                   Scenario: "process.basic",
                                                                                   Sql: "SELECT 1",
                                                                                   RowCount: -1));

    [TestMethod]
    public void SeedFixtureBatchHasher_IsStableForSameBatch()
    {
        var batch = CreateBatch(sql: "INSERT INTO x VALUES (1), (2);", rowCount: 2);

        var first = batch.ContentHash;
        var second = SeedFixtureBatchHasher.Hash(batch);

        Assert.AreEqual(first, second);
        Assert.AreEqual(64, first.Length);
    }

    [TestMethod]
    public void SeedFixtureBatchHasher_NormalizesSqlWhitespace()
    {
        var first = CreateBatch(sql: "INSERT INTO x VALUES (1), (2);", rowCount: 2);
        var second = CreateBatch(sql: """
                                      INSERT   INTO   x
                                      VALUES
                                          (1),
                                          (2);
                                      """, rowCount: 2);

        Assert.AreEqual(first.ContentHash, second.ContentHash);
    }

    [TestMethod]
    public void SeedFixtureBatchHasher_ChangesWhenRowCountChanges()
    {
        var first = CreateBatch(sql: "INSERT INTO x VALUES (1), (2);", rowCount: 2);
        var second = CreateBatch(sql: "INSERT INTO x VALUES (1), (2);", rowCount: 3);

        Assert.AreNotEqual(first.ContentHash, second.ContentHash);
    }

    [TestMethod]
    public void SeedFixtureBatchHasher_ChangesWhenScenarioChanges()
    {
        var first = CreateBatch(scenario: "process.basic");
        var second = CreateBatch(scenario: "process.persistence");

        Assert.AreNotEqual(first.ContentHash, second.ContentHash);
    }

    [TestMethod]
    public void SeedFixtureBatchFactory_CreatesDeterministicBatchesFromPerTableSeedSql()
    {
        var seedSqlByTable = new Dictionary<string, string>
        {
            ["bronze.windows_sysmon_event"] = "INSERT INTO bronze.windows_sysmon_event VALUES (1);",
            ["bronze.dns_server_event"] = "INSERT INTO bronze.dns_server_event VALUES (1), (2);"
        };

        var expectedRowsByTable = new Dictionary<string, long>
        {
            ["bronze.windows_sysmon_event"] = 1,
            ["bronze.dns_server_event"] = 2
        };

        var sourceNameByTable = new Dictionary<string, string>
        {
            ["bronze.windows_sysmon_event"] = "Windows Sysmon",
            ["bronze.dns_server_event"] = "DNS Server"
        };

        var batches = SeedFixtureBatchFactory.FromTableSeedSql(
            seedSqlByTable,
            expectedRowsByTable,
            sourceNameByTable,
            scenario: "development.baseline",
            catalogVersion: "phase-1c");

        CollectionAssert.AreEqual(
            new[]
            {
                "bronze_dns_server_event_development_baseline",
                "bronze_windows_sysmon_event_development_baseline"
            },
            batches.Select(static batch => batch.BatchId).ToArray());

        Assert.IsTrue(batches.All(static batch => batch.ContentHash.Length == 64));
        Assert.IsTrue(batches.All(static batch => batch.CatalogVersion == "phase-1c"));
    }

    [TestMethod]
    public void SeedFixtureBatchFactory_FailsWhenExpectedRowCountIsMissing()
    {
        var ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            SeedFixtureBatchFactory.FromTableSeedSql(
                new Dictionary<string, string>
                {
                    ["bronze.windows_sysmon_event"] = "INSERT INTO bronze.windows_sysmon_event VALUES (1);"
                },
                new Dictionary<string, long>(),
                new Dictionary<string, string>
                {
                    ["bronze.windows_sysmon_event"] = "Windows Sysmon"
                },
                scenario: "development.baseline"));

        Assert.Contains("Missing expected row count", ex.Message);
    }

    [TestMethod]
    public void SeedFixtureBatchFactory_FailsWhenSourceNameIsMissing()
    {
        var ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            SeedFixtureBatchFactory.FromTableSeedSql(
                new Dictionary<string, string>
                {
                    ["bronze.windows_sysmon_event"] = "INSERT INTO bronze.windows_sysmon_event VALUES (1);"
                },
                new Dictionary<string, long>
                {
                    ["bronze.windows_sysmon_event"] = 1
                },
                new Dictionary<string, string>(),
                scenario: "development.baseline"));

        Assert.Contains("Missing source name", ex.Message);
    }

    private static SeedFixtureBatch CreateBatch(
        string sql = "INSERT INTO x VALUES (1);",
        long rowCount = 1,
        string scenario = "process.basic") =>
        new(
            BatchId: "batch",
            TableName: "bronze.windows_sysmon_event",
            SourceName: "Windows Sysmon",
            Scenario: scenario,
            Sql: sql,
            RowCount: rowCount,
            CatalogVersion: "phase-1c");
}