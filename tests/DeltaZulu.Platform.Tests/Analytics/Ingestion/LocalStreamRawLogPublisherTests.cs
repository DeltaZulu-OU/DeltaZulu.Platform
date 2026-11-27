using DeltaZulu.LocalStream;
using DeltaZulu.Platform.Ingestion.PubSub;

namespace DeltaZulu.Platform.Tests.Analytics.Ingestion;

[TestClass]
public sealed class LocalStreamRawLogPublisherTests
{
    [TestMethod]
    public async Task PublishAsync_AppendsAllEventsAndPreservesSourceEnvelope()
    {
        var storage = Path.Combine(Path.GetTempPath(), "deltazulu-localstream-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var host = new LocalStreamHost(new LocalStreamOptions
            {
                StoragePath = storage,
                Topics = { new TopicOptions { Name = "agent.raw" } }
            });
            await host.StartAsync(TestContext.CancellationToken);
            var publisher = new LocalStreamRawLogPublisher(host, "agent.raw");
            var input = new RawLogEnvelope(
                "bronze.windows_sysmon_event",
                new DateTimeOffset(2026, 11, 27, 10, 00, 00, TimeSpan.Zero),
                "windows-sysmon",
                "Microsoft-Windows-Sysmon",
                "WS-001",
                "{\"EventID\":1}");
            var batch = new RawLogBatch("batch-1", input.Channel, [input, input with { RawLog = "{\"EventID\":2}" }]);

            await publisher.PublishAsync(batch, TestContext.CancellationToken);

            var consumer = host.CreateConsumer<RawLogEnvelope>("test.reader");
            var records = new List<StreamRecord<RawLogEnvelope>>();
            await foreach (var record in consumer.ReadAsync("agent.raw", cancellationToken: TestContext.CancellationToken))
                records.Add(record);

            Assert.HasCount(2, records);
            Assert.AreEqual(input.Channel, records[0].Payload.Channel);
            Assert.AreEqual(input.IngestTimeUtc, records[0].Payload.IngestTimeUtc);
            Assert.AreEqual(input.SourceName, records[0].Payload.SourceName);
            Assert.AreEqual(input.Provider, records[0].Payload.Provider);
            Assert.AreEqual(input.Host, records[0].Payload.Host);
            Assert.AreEqual(input.RawLog, records[0].Payload.RawLog);
            Assert.AreNotEqual(records[0].EventId, records[1].EventId);
        }
        finally
        {
            if (Directory.Exists(storage))
                Directory.Delete(storage, recursive: true);
        }
    }

    public TestContext TestContext { get; set; } = null!;
}
