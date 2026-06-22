using DeltaZulu.Platform.Data.Seeding;
using DeltaZulu.Platform.Ingestion.Ndjson;
using DeltaZulu.Platform.Ingestion.PubSub;

namespace DeltaZulu.Platform.Tests.Analytics.Ingestion;

[TestClass]
public sealed class RawLogPubSubTests
{
    [TestMethod]
    public void RawLogNdjsonCodec_RoundTripsRawLogEnvelope()
    {
        var input = new RawLogEnvelope(
            "bronze.windows_sysmon_event",
            new DateTimeOffset(2026, 06, 22, 10, 30, 00, TimeSpan.Zero),
            "windows-sysmon",
            "Microsoft-Windows-Sysmon",
            "WS-001",
            "{\"EventID\":\"1\",\"Image\":\"cmd.exe\"}");

        var ndjson = RawLogNdjsonCodec.Write([input]);
        var events = RawLogNdjsonCodec.Read(ndjson);

        Assert.HasCount(1, events);
        Assert.AreEqual(input.Channel, events[0].Channel);
        Assert.AreEqual(input.SourceName, events[0].SourceName);
        Assert.AreEqual(input.Provider, events[0].Provider);
        Assert.AreEqual(input.Host, events[0].Host);
        Assert.Contains("cmd.exe", events[0].RawLog);
    }


    [TestMethod]
    public void RawLogNdjsonCodec_PoolsRepeatedMetadataStrings()
    {
        var first = new RawLogEnvelope(
            "bronze.windows_sysmon_event",
            new DateTimeOffset(2026, 06, 22, 10, 30, 00, TimeSpan.Zero),
            "windows-sysmon",
            "Microsoft-Windows-Sysmon",
            "WS-001",
            "{\"EventID\":\"1\"}");
        var second = first with { RawLog = "{\"EventID\":\"2\"}" };

        var events = RawLogNdjsonCodec.Read(RawLogNdjsonCodec.Write([first, second]));

        Assert.AreEqual(2, events.Count);
        Assert.IsTrue(ReferenceEquals(events[0].Channel, events[1].Channel));
        Assert.IsTrue(ReferenceEquals(events[0].SourceName, events[1].SourceName));
        Assert.IsTrue(ReferenceEquals(events[0].Provider, events[1].Provider));
        Assert.IsTrue(ReferenceEquals(events[0].Host, events[1].Host));
    }

    [TestMethod]
    public void InMemoryRawLogBus_PublishesBatchToMatchingChannelSubscriber()
    {
        var subscriber = new CapturingSubscriber();
        var bus = new InMemoryRawLogBus();
        bus.Subscribe("bronze.windows_sysmon_event", subscriber);

        var batch = new RawLogBatch(
            "batch-1",
            "bronze.windows_sysmon_event",
            [new RawLogEnvelope(
                "bronze.windows_sysmon_event",
                DateTimeOffset.UtcNow,
                "windows-sysmon",
                "Microsoft-Windows-Sysmon",
                "WS-001",
                "{\"EventID\":\"1\"}")]);

        bus.PublishAsync(batch).AsTask().GetAwaiter().GetResult();

        Assert.AreSame(batch, subscriber.LastBatch);
    }


    [TestMethod]
    public void InMemoryRawLogBus_IgnoresDuplicateSubscriberRegistration()
    {
        var subscriber = new CountingSubscriber();
        var bus = new InMemoryRawLogBus();
        bus.Subscribe("bronze.windows_sysmon_event", subscriber);
        bus.Subscribe("bronze.windows_sysmon_event", subscriber);

        var batch = new RawLogBatch(
            "batch-1",
            "bronze.windows_sysmon_event",
            [new RawLogEnvelope(
                "bronze.windows_sysmon_event",
                DateTimeOffset.UtcNow,
                "windows-sysmon",
                "Microsoft-Windows-Sysmon",
                "WS-001",
                "{\"EventID\":\"1\"}")]);

        bus.PublishAsync(batch).AsTask().GetAwaiter().GetResult();

        Assert.AreEqual(1, subscriber.Count);
    }

    [TestMethod]
    public void MockDataSeeder_ExposesDevelopmentRawLogsAsNdjsonChannels()
    {
        var ndjsonByChannel = MockDataSeeder.GetMedallionSeedNdjsonByChannel(
            new DateTimeOffset(2026, 06, 22, 00, 00, 00, TimeSpan.Zero));

        Assert.Contains("bronze.windows_sysmon_event", ndjsonByChannel.Keys.ToArray());
        var sysmonEvents = RawLogNdjsonCodec.Read(ndjsonByChannel["bronze.windows_sysmon_event"]);

        Assert.AreEqual(MockDataSeeder.GetExpectedMedallionRowCountsByTable()["bronze.windows_sysmon_event"], sysmonEvents.Count);
        Assert.IsTrue(sysmonEvents.All(static item => item.Channel == "bronze.windows_sysmon_event"));
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

    private sealed class CapturingSubscriber : IRawLogSubscriber
    {
        public RawLogBatch? LastBatch { get; private set; }

        public ValueTask HandleAsync(RawLogBatch batch, CancellationToken cancellationToken = default)
        {
            LastBatch = batch;
            return ValueTask.CompletedTask;
        }
    }
}
