using DeltaZulu.LocalStream;
using DeltaZulu.Platform.Data.DuckDb;
using DeltaZulu.Platform.Data.DuckDb.Ingestion;
using DeltaZulu.Platform.Ingestion.PubSub;

namespace DeltaZulu.Platform.Tests.Analytics.Ingestion;

[TestClass]
public sealed class LocalStreamBronzeWriterTests
{
    [TestMethod]
    public async Task Consumer_RestartAfterLakeCommitBeforeCheckpointDoesNotDuplicate()
    {
        var root = Path.Combine(Path.GetTempPath(), "dz-restart-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            LocalStreamHost CreateHost() => new(new LocalStreamOptions
            {
                StoragePath = Path.Combine(root, "stream"),
                Topics = { new TopicOptions { Name = "agent.raw" } },
                Subscriptions = { new SubscriptionOptions { Id = "lake.bronze", Topic = "agent.raw", Required = true } }
            });
            var connectionString = "DataSource=" + Path.Combine(root, "lake.db");
            var routes = new Dictionary<string, string> { ["raw.test"] = "bronze.test_raw" };
            await using (var host = CreateHost())
            {
                await host.StartAsync();
                using var factory = new DuckDbConnectionFactory(connectionString, startupSql: []);
                new SchemaApplier(factory).ExecuteRaw("CREATE SCHEMA bronze; CREATE TABLE bronze.test_raw (ingest_time TIMESTAMP, source_name VARCHAR, provider VARCHAR, host VARCHAR, raw_log JSON, raw_text VARCHAR);");
                using var writer = new LocalStreamBronzeWriter(factory, routes);
                var publisher = new LocalStreamRawLogPublisher(host, "agent.raw");
                var payload = new RawLogEnvelope("raw.test", DateTimeOffset.UtcNow, "test", "provider", "host", "{}");
                await publisher.PublishAsync(new RawLogBatch("batch-1", payload.Channel, [payload]));
                var reader = host.CreateConsumer<RawLogEnvelope>("lake.bronze");
                var count = 0;
                await foreach (var entry in reader.ReadAsync("agent.raw"))
                {
                    await writer.WriteAsync(entry);
                    count++;
                    // Deliberately leave the checkpoint uncommitted.
                }
                Assert.AreEqual(1, count);
            }
            await using (var host = CreateHost())
            {
                await host.StartAsync();
                using var factory = new DuckDbConnectionFactory(connectionString, startupSql: []);
                using var writer = new LocalStreamBronzeWriter(factory, routes);
                var consumer = new LocalStreamBronzeConsumer(host, writer, "agent.raw", "lake.bronze");
                Assert.AreEqual(1, await consumer.DrainOnceAsync());
                Assert.AreEqual(0, await consumer.DrainOnceAsync());
                Assert.AreEqual(1L, new SchemaApplier(factory).QueryScalar("SELECT count(*) FROM bronze.test_raw"));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task WriteAsync_InvalidJsonRollsBackAndAllowsRetry()
    {
        using var factory = new DuckDbConnectionFactory("DataSource=:memory:", startupSql: []);
        var applier = new SchemaApplier(factory);
        applier.ExecuteRaw("CREATE SCHEMA bronze; CREATE TABLE bronze.test_raw (ingest_time TIMESTAMP, source_name VARCHAR, provider VARCHAR, host VARCHAR, raw_log JSON, raw_text VARCHAR);");
        using var writer = new LocalStreamBronzeWriter(factory, new Dictionary<string, string> { ["raw.test"] = "bronze.test_raw" });
        StreamRecord<RawLogEnvelope> Entry(string json, long offset) => new()
        {
            Topic = "agent.raw", Partition = 0, Offset = offset, EventId = $"event-{offset}",
            PublishedUtc = DateTimeOffset.UtcNow, Headers = new Dictionary<string, string>(),
            Payload = new RawLogEnvelope("raw.test", DateTimeOffset.UtcNow, "test", "provider", "host", json)
        };
        await writer.WriteAsync(Entry("{}", 0));
        await Assert.ThrowsExactlyAsync<DuckDB.NET.Data.DuckDBException>(
            async () => await writer.WriteAsync(Entry("invalid-json", 1)));
        Assert.AreEqual(1L, applier.QueryScalar("SELECT count(*) FROM bronze.test_raw"));
        Assert.AreEqual(1L, applier.QueryScalar("SELECT count(*) FROM internal.bronze_stream_receipts"));
        await writer.WriteAsync(Entry("{}", 1));
        Assert.AreEqual(2L, applier.QueryScalar("SELECT count(*) FROM bronze.test_raw"));
        Assert.AreEqual(2L, applier.QueryScalar("SELECT count(*) FROM internal.bronze_stream_receipts"));
    }

    [TestMethod]
    public async Task WriteAsync_ReplayAfterReopenDoesNotDuplicateOccurrence()
    {
        var root = Path.Combine(Path.GetTempPath(), "dz-receipts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var connectionString = "DataSource=" + Path.Combine(root, "lake.db");
            var routes = new Dictionary<string, string> { ["raw.test"] = "bronze.test_raw" };
            var payload = new RawLogEnvelope("raw.test", DateTimeOffset.UtcNow, "test", "provider", "host", "{}");
            var entry = new StreamRecord<RawLogEnvelope>
            {
                Topic = "agent.raw", Partition = 0, Offset = 0, EventId = "event-1",
                PublishedUtc = DateTimeOffset.UtcNow, Headers = new Dictionary<string, string>(), Payload = payload
            };
            using (var factory = new DuckDbConnectionFactory(connectionString, startupSql: []))
            {
                var applier = new SchemaApplier(factory);
                applier.ExecuteRaw("CREATE SCHEMA bronze; CREATE TABLE bronze.test_raw (ingest_time TIMESTAMP, source_name VARCHAR, provider VARCHAR, host VARCHAR, raw_log JSON, raw_text VARCHAR);");
                using var writer = new LocalStreamBronzeWriter(factory, routes);
                await writer.WriteAsync(entry);
            }
            using (var factory = new DuckDbConnectionFactory(connectionString, startupSql: []))
            {
                using var writer = new LocalStreamBronzeWriter(factory, routes);
                await writer.WriteAsync(entry);
                await writer.WriteAsync(new StreamRecord<RawLogEnvelope>
                {
                    Topic = entry.Topic, Partition = 0, Offset = 1, EventId = "event-2",
                    PublishedUtc = entry.PublishedUtc, Headers = entry.Headers, Payload = payload
                });
                var applier = new SchemaApplier(factory);
                Assert.AreEqual(2L, applier.QueryScalar("SELECT count(*) FROM bronze.test_raw"));
                Assert.AreEqual(2L, applier.QueryScalar("SELECT count(*) FROM internal.bronze_stream_receipts"));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
