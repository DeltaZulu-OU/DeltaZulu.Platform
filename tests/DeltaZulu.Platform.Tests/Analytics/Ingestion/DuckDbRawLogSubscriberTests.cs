using DeltaZulu.Platform.Data.DuckDb;
using DeltaZulu.Platform.Data.DuckDb.Ingestion;
using DeltaZulu.Platform.Ingestion.PubSub;

namespace DeltaZulu.Platform.Tests.Analytics.Ingestion;

[TestClass]
public sealed class DuckDbRawLogSubscriberTests
{
    [TestMethod]
    public async Task HandleAsync_InsertsRawLogBatchIntoMappedBronzeTable()
    {
        using var factory = new DuckDbConnectionFactory("DataSource=:memory:");
        var applier = new SchemaApplier(factory);
        applier.ExecuteRaw("""
CREATE SCHEMA IF NOT EXISTS bronze;
CREATE TABLE bronze.test_raw (
    ingest_time TIMESTAMP,
    source_name VARCHAR,
    provider VARCHAR,
    host VARCHAR,
    raw_log JSON,
    raw_text VARCHAR
);
""");

        var subscriber = new DuckDbRawLogSubscriber(
            applier,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["raw.test"] = "bronze.test_raw"
            },
            maxRowsPerInsert: 1);

        await subscriber.HandleAsync(new RawLogBatch(
            "batch-1",
            "raw.test",
            [
                new RawLogEnvelope(
                    "raw.test",
                    new DateTimeOffset(2026, 06, 22, 10, 00, 00, TimeSpan.Zero),
                    "test-source",
                    "test-provider",
                    "test-host",
                    "{\"EventID\":\"1\"}"),
                new RawLogEnvelope(
                    "raw.test",
                    new DateTimeOffset(2026, 06, 22, 10, 01, 00, TimeSpan.Zero),
                    "test-source",
                    "test-provider",
                    "test-host",
                    "{\"EventID\":\"2\"}")
            ]));

        Assert.AreEqual(2L, applier.QueryScalar("SELECT count(*) FROM bronze.test_raw"));
    }
}
