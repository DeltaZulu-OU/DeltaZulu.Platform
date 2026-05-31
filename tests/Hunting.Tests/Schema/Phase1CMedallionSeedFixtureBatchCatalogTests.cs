namespace Hunting.Tests.Schema;

using Hunting.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class Phase1CMedallionSeedFixtureBatchCatalogTests
{
    private static readonly string[] ExpectedBatchIds =
    [
        "bronze_dns_server_event_development_baseline",
        "bronze_windows_security_event_development_baseline",
        "bronze_windows_sysmon_event_development_baseline"
    ];

    private static readonly string[] ExpectedTables =
    [
        "bronze.dns_server_event",
        "bronze.windows_security_event",
        "bronze.windows_sysmon_event"
    ];

    [TestMethod]
    public void MockDataSeeder_ExposesOneGovernedSeedFixtureBatchPerActiveBronzeTable()
    {
        var batches = MockDataSeeder.GetMedallionSeedFixtureBatches();

        CollectionAssert.AreEqual(ExpectedBatchIds, batches.Select(static batch => batch.BatchId).ToArray());
        CollectionAssert.AreEqual(ExpectedTables, batches.Select(static batch => batch.TableName).ToArray());
    }

    [TestMethod]
    public void MockDataSeeder_SeedFixtureBatchesMatchExistingPerTableSeedSqlAndExpectedCounts()
    {
        var batches = MockDataSeeder.GetMedallionSeedFixtureBatches();
        var seedSqlByTable = MockDataSeeder.GetMedallionSeedSqlByTable();
        var expectedRowsByTable = MockDataSeeder.GetExpectedMedallionRowCountsByTable();

        foreach (var batch in batches)
        {
            Assert.AreEqual(seedSqlByTable[batch.TableName].Trim(), batch.Sql);
            Assert.AreEqual(expectedRowsByTable[batch.TableName], batch.RowCount);
            Assert.AreEqual("development.baseline", batch.Scenario);
            Assert.AreEqual(64, batch.ContentHash.Length);
        }
    }

    [TestMethod]
    public void MockDataSeeder_SeedFixtureBatchesCarryExpectedSourceNames()
    {
        var batches = MockDataSeeder.GetMedallionSeedFixtureBatches()
            .ToDictionary(static batch => batch.TableName, StringComparer.OrdinalIgnoreCase);

        Assert.AreEqual("Windows Sysmon", batches["bronze.windows_sysmon_event"].SourceName);
        Assert.AreEqual("Windows Security", batches["bronze.windows_security_event"].SourceName);
        Assert.AreEqual("DNS Server", batches["bronze.dns_server_event"].SourceName);
    }

    [TestMethod]
    public void MockDataSeeder_SeedFixtureBatchesAcceptCatalogVersion()
    {
        var batches = MockDataSeeder.GetMedallionSeedFixtureBatches("phase-1c-test");

        Assert.IsNotEmpty(batches);
        Assert.IsTrue(batches.All(static batch => batch.CatalogVersion == "phase-1c-test"));
    }

    [TestMethod]
    public void MockDataSeeder_CombinedSeedSqlContainsEveryFixtureBatchSql()
    {
        var combined = MockDataSeeder.GetMedallionSeedSql();

        foreach (var batch in MockDataSeeder.GetMedallionSeedFixtureBatches())
        {
            Assert.Contains(batch.Sql, combined);
        }
    }

    [TestMethod]
    public void MockDataSeeder_SeedFixtureBatchContentHashesAreDeterministic()
    {
        var first = MockDataSeeder.GetMedallionSeedFixtureBatches();
        var second = MockDataSeeder.GetMedallionSeedFixtureBatches();

        CollectionAssert.AreEqual(
            first.Select(static batch => batch.ContentHash).ToArray(),
            second.Select(static batch => batch.ContentHash).ToArray());
    }
}