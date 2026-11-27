using DeltaZulu.LocalStream;
using DeltaZulu.Platform.Data.DuckDb;
using DeltaZulu.Platform.Data.DuckDb.Ingestion;
using DeltaZulu.Platform.Ingestion.PubSub;

namespace DeltaZulu.Platform.Tests.Analytics.Ingestion;

[TestClass]
public sealed class LocalStreamBronzeConsumerTests
{
    [TestMethod]
    public async Task DrainOnceAsync_WritesBronzeAndCommitsOnlyAfterSuccessfulWrite()
    {
        var storage = Path.Combine(Path.GetTempPath(), "deltazulu-bronze-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var host = new LocalStreamHost(new LocalStreamOptions
            {
                StoragePath = storage,
                Topics = { new TopicOptions { Name = "agent.raw" } },
                Subscriptions = { new SubscriptionOptions { Id = "lake.bronze", Topic = "agent.raw", Required = true } }
            });
            await host.StartAsync(TestContext.CancellationToken);
            using var factory = new DuckDbConnectionFactory("DataSource=:memory:");
            var applier = new SchemaApplier(factory);
            applier.ExecuteRaw("""
CREATE SCHEMA IF NOT EXISTS bronze;
CREATE TABLE bronze.test_raw (ingest_time TIMESTAMP, source_name VARCHAR, provider VARCHAR, host VARCHAR, raw_log JSON, raw_text VARCHAR);
""");
            using var lake = new DuckDbRawLogSubscriber(applier, new Dictionary<string, string> { ["raw.test"] = "bronze.test_raw" });
            var publisher = new LocalStreamRawLogPublisher(host, "agent.raw");
            var input = new RawLogEnvelope("raw.test", DateTimeOffset.UtcNow, "test", "test-provider", "test-host", "{\"EventID\":1}");
            await publisher.PublishAsync(new RawLogBatch("batch-1", input.Channel, [input]), TestContext.CancellationToken);
            var consumer = new LocalStreamBronzeConsumer(host, lake, "agent.raw", "lake.bronze");

            Assert.AreEqual(1, await consumer.DrainOnceAsync(TestContext.CancellationToken));
            Assert.AreEqual(1L, applier.QueryScalar("SELECT count(*) FROM bronze.test_raw"));
            Assert.AreEqual(0, await consumer.DrainOnceAsync(TestContext.CancellationToken));
            Assert.AreEqual(1L, applier.QueryScalar("SELECT count(*) FROM bronze.test_raw"));
        }
        finally
        {
            if (Directory.Exists(storage)) Directory.Delete(storage, recursive: true);
        }
    }

    [TestMethod]
    public async Task DrainOnceAsync_FailedWriteLeavesRecordAvailableForReplay()
    {
        var storage = Path.Combine(Path.GetTempPath(), "deltazulu-bronze-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var host = new LocalStreamHost(new LocalStreamOptions
            {
                StoragePath = storage,
                Topics = { new TopicOptions { Name = "agent.raw" } }
            });
            await host.StartAsync(TestContext.CancellationToken);
            var publisher = new LocalStreamRawLogPublisher(host, "agent.raw");
            var input = new RawLogEnvelope("raw.test", DateTimeOffset.UtcNow, "test", "provider", "host", "{\"EventID\":1}");
            await publisher.PublishAsync(new RawLogBatch("batch-1", "raw.test", [input]), TestContext.CancellationToken);
            var failing = new LocalStreamBronzeConsumer(host, new ThrowingSubscriber(), "agent.raw", "lake.bronze");
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => failing.DrainOnceAsync(TestContext.CancellationToken));

            var successful = new CountingSubscriber();
            var retry = new LocalStreamBronzeConsumer(host, successful, "agent.raw", "lake.bronze");
            Assert.AreEqual(1, await retry.DrainOnceAsync(TestContext.CancellationToken));
            Assert.AreEqual(1, successful.Count);
            Assert.AreEqual(0, await retry.DrainOnceAsync(TestContext.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(storage)) Directory.Delete(storage, recursive: true);
        }
    }

    private sealed class ThrowingSubscriber : IRawLogSubscriber
    {
        public ValueTask HandleAsync(RawLogBatch batch, CancellationToken cancellationToken = default)
            => ValueTask.FromException(new InvalidOperationException("Simulated write failure"));
    }

    private sealed class CountingSubscriber : IRawLogSubscriber
    {
        public int Count { get; private set; }
        public ValueTask HandleAsync(RawLogBatch batch, CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.CompletedTask;
        }
    }

    public TestContext TestContext { get; set; } = null!;
}
