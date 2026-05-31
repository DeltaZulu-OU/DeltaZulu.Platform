namespace Hunting.Tests.Schema;

using Hunting.Core.DuckDbSql;
using Hunting.Data;
using Hunting.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class Phase1CSeedFixtureBatchRecordingTests
{
    [TestMethod]
    public void SeedFixtureBatchRecorder_CanDetectMatchingRecordedBatch()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        CreateAndApplySchema(factory);

        var recorder = new SeedFixtureBatchRecorder(factory);
        var batches = MockDataSeeder.GetMedallionSeedFixtureBatches();
        var firstBatch = batches[0];

        Assert.IsFalse(recorder.HasMatchingRecordedBatch(firstBatch));

        recorder.RecordAppliedSeedBatches(batches);

        Assert.IsTrue(recorder.HasMatchingRecordedBatch(firstBatch));
    }

    [TestMethod]
    public void SeedFixtureBatchRecorder_DoesNotExecuteSeedSql()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);
        var recorder = new SeedFixtureBatchRecorder(factory);

        var batches = MockDataSeeder.GetMedallionSeedFixtureBatches();
        recorder.RecordAppliedSeedBatches(batches);

        foreach (var tableName in MockDataSeeder.GetExpectedMedallionRowCountsByTable().Keys)
        {
            Assert.AreEqual(0, applier.QueryScalar($"SELECT count(*) FROM {tableName}"));
        }
    }

    [TestMethod]
    public void SeedFixtureBatchRecorder_ReapplyIsIdempotentByBatchId()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);
        var recorder = new SeedFixtureBatchRecorder(factory);

        var batches = MockDataSeeder.GetMedallionSeedFixtureBatches();

        recorder.RecordAppliedSeedBatches(batches, catalogVersion: "first");
        var firstCount = applier.QueryScalar("SELECT count(*) FROM internal.seed_batches");

        recorder.RecordAppliedSeedBatches(batches, catalogVersion: "second");
        var secondCount = applier.QueryScalar("SELECT count(*) FROM internal.seed_batches");

        var rows = recorder.ReadRecordedSeedBatches();

        Assert.AreEqual(firstCount, secondCount);
        Assert.IsTrue(rows.All(static row => row.CatalogVersion == "second"));
    }

    [TestMethod]
    public void SeedFixtureBatchRecorder_RecordsExpectedBatchMetadata()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        CreateAndApplySchema(factory);

        var recorder = new SeedFixtureBatchRecorder(factory);
        var batches = MockDataSeeder.GetMedallionSeedFixtureBatches("phase-1c-test");

        recorder.RecordAppliedSeedBatches(batches);

        var rows = recorder.ReadRecordedSeedBatches();
        var byTable = rows.ToDictionary(static row => row.TableName, StringComparer.OrdinalIgnoreCase);

        Assert.AreEqual("Windows Sysmon", byTable["bronze.windows_sysmon_event"].SourceName);
        Assert.AreEqual("Windows Security", byTable["bronze.windows_security_event"].SourceName);
        Assert.AreEqual("DNS Server", byTable["bronze.dns_server_event"].SourceName);

        Assert.IsTrue(rows.All(static row => row.Scenario == "development.baseline"));
        Assert.IsTrue(rows.All(static row => row.ContentHash.Length == 64));
        Assert.IsTrue(rows.All(static row => row.CatalogVersion == "phase-1c-test"));
    }

    [TestMethod]
    public void SeedFixtureBatchRecorder_RecordsOneRowPerGovernedMedallionBatch()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);
        var recorder = new SeedFixtureBatchRecorder(factory);

        var batches = MockDataSeeder.GetMedallionSeedFixtureBatches("phase-1c-test");
        var rows = recorder.RecordAppliedSeedBatches(batches);

        Assert.HasCount(batches.Count, rows);
        Assert.AreEqual(batches.Count, applier.QueryScalar("SELECT count(*) FROM internal.seed_batches"));
    }

    [TestMethod]
    public void SeedFixtureBatchRecorder_UpdateByBatchIdReplacesChangedContentHash()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        CreateAndApplySchema(factory);

        var recorder = new SeedFixtureBatchRecorder(factory);
        var batch = new SeedFixtureBatch(
            BatchId: "test_batch",
            TableName: "bronze.windows_sysmon_event",
            SourceName: "Windows Sysmon",
            Scenario: "development.baseline",
            Sql: "INSERT INTO bronze.windows_sysmon_event VALUES (1);",
            RowCount: 1);

        recorder.RecordAppliedSeedBatches([batch]);
        var originalHash = recorder.ReadRecordedSeedBatches().Single(static row => row.BatchId == "test_batch").ContentHash;

        var changed = new SeedFixtureBatch(
             BatchId: batch.BatchId,
             TableName: batch.TableName,
             SourceName: batch.SourceName,
             Scenario: batch.Scenario,
             Sql: "INSERT INTO bronze.windows_sysmon_event VALUES (1), (2);",
             RowCount: 2,
             CatalogVersion: batch.CatalogVersion);

        recorder.RecordAppliedSeedBatches([changed]);
        var rows = recorder.ReadRecordedSeedBatches().Where(static row => row.BatchId == "test_batch").ToArray();

        Assert.HasCount(1, rows);
        Assert.AreNotEqual(originalHash, rows[0].ContentHash);
        Assert.AreEqual(2, rows[0].RowCount);
    }

    [TestMethod]
    public void SeedFixtureBatchRecorder_UsesBatchSpecificCatalogVersionWhenProvided()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        CreateAndApplySchema(factory);

        var recorder = new SeedFixtureBatchRecorder(factory);
        var batches = MockDataSeeder.GetMedallionSeedFixtureBatches("batch-version");

        recorder.RecordAppliedSeedBatches(batches, catalogVersion: "fallback-version");

        var rows = recorder.ReadRecordedSeedBatches();

        Assert.IsNotEmpty(rows);
        Assert.IsTrue(rows.All(static row => row.CatalogVersion == "batch-version"));
    }

    private static SchemaApplier CreateAndApplySchema(DuckDbConnectionFactory factory)
    {
        var applier = new SchemaApplier(factory);
        var ddl = new SchemaEmitter().EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: SchemaConventions.InternalTables,
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        applier.ApplyStatements(ddl);
        return applier;
    }
}