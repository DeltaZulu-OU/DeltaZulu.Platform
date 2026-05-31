namespace Hunting.Tests.Schema;

using Hunting.Core.DuckDbSql;
using Hunting.Data;
using Hunting.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class Phase1CSeedFixtureBatchApplyTests
{
    [TestMethod]
    public void SeedFixtureBatchApplier_AllowMismatchedPolicyAppliesAndReplacesMetadata()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);
        var recorder = new SeedFixtureBatchRecorder(factory);
        var batchApplier = new SeedFixtureBatchApplier(applier, recorder);

        var original = new SeedFixtureBatch(
            BatchId: "custom_batch",
            TableName: "bronze.dns_server_event",
            SourceName: "DNS Server",
            Scenario: "development.custom",
            Sql:
            """
            INSERT INTO bronze.dns_server_event (ingest_time, source_name, provider, host, raw_log, raw_text)
            VALUES (TIMESTAMP '2024-06-15 09:30:00', 'dns-server', 'Technitium DNS Server', 'DNS-001',
                    CAST('{"opcode":"QUERY","query_name":"custom.example.test"}' AS JSON), '')
            """,
            RowCount: 1);

        batchApplier.Apply([original]);

        var changed = new SeedFixtureBatch(
            BatchId: original.BatchId,
            TableName: original.TableName,
            SourceName: original.SourceName,
            Scenario: original.Scenario,
            Sql:
            """
            INSERT INTO bronze.dns_server_event (ingest_time, source_name, provider, host, raw_log, raw_text)
            VALUES (TIMESTAMP '2024-06-15 09:31:00', 'dns-server', 'Technitium DNS Server', 'DNS-001',
                    CAST('{"opcode":"QUERY","query_name":"changed.example.test"}' AS JSON), '')
            """,
            RowCount: 1);

        var report = batchApplier.Apply([changed], SeedFixtureBatchApplyPolicy.AllowMismatchedRecordedBatch);

        Assert.AreEqual(1, report.AppliedCount);
        Assert.AreEqual(0, report.BlockedCount);

        var recorded = recorder.ReadRecordedSeedBatches().Single(static row => row.BatchId == "custom_batch");
        Assert.AreEqual(changed.ContentHash, recorded.ContentHash);
    }

    [TestMethod]
    public void SeedFixtureBatchApplier_AppliesAndRecordsMissingBatches()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);
        var batchApplier = CreateBatchApplier(factory, applier);

        var batches = MockDataSeeder.GetMedallionSeedFixtureBatches("phase-1c-test");
        var report = batchApplier.Apply(batches);

        Assert.AreEqual(batches.Count, report.AppliedCount);
        Assert.AreEqual(0, report.SkippedCount);
        Assert.AreEqual(0, report.BlockedCount);

        foreach (var (tableName, expectedRows) in MockDataSeeder.GetExpectedMedallionRowCountsByTable())
        {
            Assert.AreEqual(expectedRows, applier.QueryScalar($"SELECT count(*) FROM {tableName}"), tableName);
        }

        Assert.AreEqual(batches.Count, applier.QueryScalar("SELECT count(*) FROM internal.seed_batches"));
    }

    [TestMethod]
    public void SeedFixtureBatchApplier_BlocksMismatchedRecordedBatchByDefault()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);
        var batchApplier = CreateBatchApplier(factory, applier);

        var batches = MockDataSeeder.GetMedallionSeedFixtureBatches();
        batchApplier.Apply(batches);

        var changedBatch = CreateChangedBatch(batches[0]);

        var ex = Assert.ThrowsExactly<SeedFixtureBatchMismatchException>(() =>
            batchApplier.Apply([changedBatch]));

        Assert.IsTrue(ex.Report.HadBlockedBatch);
        Assert.AreEqual(1, ex.Report.BlockedCount);
        Assert.AreEqual(0, ex.Report.AppliedCount);
    }

    [TestMethod]
    public void SeedFixtureBatchApplier_DoesNotDuplicateRowsOnRepeatedApply()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);
        var batchApplier = CreateBatchApplier(factory, applier);

        var batches = MockDataSeeder.GetMedallionSeedFixtureBatches();

        batchApplier.Apply(batches);
        var firstCounts = MockDataSeeder.GetExpectedMedallionRowCountsByTable()
            .ToDictionary(
                static item => item.Key,
                item => applier.QueryScalar($"SELECT count(*) FROM {item.Key}"),
                StringComparer.OrdinalIgnoreCase);

        batchApplier.Apply(batches);
        var secondCounts = MockDataSeeder.GetExpectedMedallionRowCountsByTable()
            .ToDictionary(
                static item => item.Key,
                item => applier.QueryScalar($"SELECT count(*) FROM {item.Key}"),
                StringComparer.OrdinalIgnoreCase);

        CollectionAssert.AreEqual(firstCounts.Keys.ToArray(), secondCounts.Keys.ToArray());

        foreach (var tableName in firstCounts.Keys)
        {
            Assert.AreEqual(firstCounts[tableName], secondCounts[tableName], tableName);
        }
    }

    [TestMethod]
    public void SeedFixtureBatchApplier_ReapplySkipsMatchingRecordedBatches()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);
        var batchApplier = CreateBatchApplier(factory, applier);

        var batches = MockDataSeeder.GetMedallionSeedFixtureBatches();

        var first = batchApplier.Apply(batches);
        var second = batchApplier.Apply(batches);

        Assert.AreEqual(batches.Count, first.AppliedCount);
        Assert.AreEqual(0, first.SkippedCount);

        Assert.AreEqual(0, second.AppliedCount);
        Assert.AreEqual(batches.Count, second.SkippedCount);

        foreach (var (tableName, expectedRows) in MockDataSeeder.GetExpectedMedallionRowCountsByTable())
        {
            Assert.AreEqual(expectedRows, applier.QueryScalar($"SELECT count(*) FROM {tableName}"), tableName);
        }
    }

    [TestMethod]
    public void SeedFixtureBatchApplier_RecordsBatchSpecificCatalogVersion()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);
        var recorder = new SeedFixtureBatchRecorder(factory);
        var batchApplier = new SeedFixtureBatchApplier(applier, recorder);

        var batches = MockDataSeeder.GetMedallionSeedFixtureBatches("batch-version");
        batchApplier.Apply(batches, catalogVersion: "fallback-version");

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

    private static SeedFixtureBatchApplier CreateBatchApplier(
            DuckDbConnectionFactory factory,
        SchemaApplier schemaApplier) =>
        new(schemaApplier, new SeedFixtureBatchRecorder(factory));

    private static SeedFixtureBatch CreateChangedBatch(SeedFixtureBatch original) =>
        new(
            BatchId: original.BatchId,
            TableName: original.TableName,
            SourceName: original.SourceName,
            Scenario: original.Scenario,
            Sql: original.Sql + Environment.NewLine + "-- changed",
            RowCount: original.RowCount,
            CatalogVersion: original.CatalogVersion);
}