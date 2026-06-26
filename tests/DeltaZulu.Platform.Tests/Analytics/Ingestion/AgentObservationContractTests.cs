using System.Text.Json;
using DeltaZulu.Platform.Ingestion.Observability;

namespace DeltaZulu.Platform.Tests.Analytics.Ingestion;

[TestClass]
public sealed class AgentObservationContractTests
{
    [TestMethod]
    public void LogUtilizationLogKey_DoesNotCollapseToEventIdOnly()
    {
        var key = new LogUtilizationLogKey(
            "WindowsEventLog",
            "Security",
            "Microsoft-Windows-Security-Auditing",
            4688);

        Assert.AreEqual(
            "WindowsEventLog:Security:Microsoft-Windows-Security-Auditing:4688",
            key.ToCanonicalString());
        Assert.AreEqual("WindowsEventLog:Security:4688", key.ToCanonicalString(includeProviderWhenAvailable: false));
    }

    [TestMethod]
    public void PipelineCountObservation_SerializesPlannedRecordKindAndFields()
    {
        var observation = new PipelineCountObservation(
            Metadata: WindowedMetadata(),
            Body: new PipelineCountObservationBody(
                "WindowsEventLog",
                "Security",
                "Microsoft-Windows-Security-Auditing",
                4688,
                ReadCount: 1200,
                KeptAfterFilterCount: 1200,
                DiscardedCount: 0,
                ForwardedCount: 1190,
                ForwardFailedCount: 5,
                PendingForwardCount: 5));

        var json = AgentObservationJsonCodec.Write(observation);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var metadata = root.GetProperty("metadata");
        var body = root.GetProperty("body");

        Assert.AreEqual(AgentObservationRecordKinds.PipelineCounts, root.GetProperty("recordKind").GetString());
        Assert.AreEqual("endpoint-123", metadata.GetProperty("agentId").GetString());
        Assert.AreEqual("host-abc", metadata.GetProperty("hostId").GetString());
        Assert.AreEqual("windows-security-default", metadata.GetProperty("profileId").GetString());
        Assert.AreEqual("2026-06-25T12:00:00+00:00", metadata.GetProperty("observedAt").GetString());
        Assert.AreEqual("2026-06-25T11:55:00+00:00", metadata.GetProperty("windowStart").GetString());
        Assert.AreEqual("2026-06-25T12:00:00+00:00", metadata.GetProperty("windowEnd").GetString());
        Assert.AreEqual("WindowsEventLog", body.GetProperty("sourceType").GetString());
        Assert.AreEqual("Security", body.GetProperty("channel").GetString());
        Assert.AreEqual("Microsoft-Windows-Security-Auditing", body.GetProperty("provider").GetString());
        Assert.AreEqual(4688, body.GetProperty("eventId").GetInt32());
        Assert.AreEqual(1200, body.GetProperty("readCount").GetInt64());
        Assert.AreEqual(1200, body.GetProperty("keptAfterFilterCount").GetInt64());
        Assert.AreEqual(0, body.GetProperty("discardedCount").GetInt64());
        Assert.AreEqual(1190, body.GetProperty("forwardedCount").GetInt64());
        Assert.AreEqual(5, body.GetProperty("forwardFailedCount").GetInt64());
        Assert.AreEqual(5, body.GetProperty("pendingForwardCount").GetInt64());

        var roundTripped = AgentObservationJsonCodec.ReadPipelineCounts(json);
        Assert.AreEqual(4688, roundTripped.Body.EventId);
        Assert.AreEqual(5, roundTripped.Body.PendingForwardCount);
    }

    [TestMethod]
    public void SourceHealthObservation_SerializesPlannedRecordKindAndFields()
    {
        var observation = new SourceHealthObservation(
            Metadata: HealthMetadata(),
            Body: new SourceHealthObservationBody(
                "WindowsEventLog",
                "Security",
                IsEnabled: true,
                CanRead: true,
                LastReadAt: new DateTimeOffset(2026, 06, 25, 11, 59, 59, TimeSpan.Zero),
                ReadErrorCount: 0));

        var json = AgentObservationJsonCodec.Write(observation);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var body = root.GetProperty("body");

        Assert.AreEqual(AgentObservationRecordKinds.SourceHealth, root.GetProperty("recordKind").GetString());
        Assert.AreEqual("WindowsEventLog", body.GetProperty("sourceType").GetString());
        Assert.AreEqual("Security", body.GetProperty("channel").GetString());
        Assert.IsTrue(body.GetProperty("isEnabled").GetBoolean());
        Assert.IsTrue(body.GetProperty("canRead").GetBoolean());
        Assert.AreEqual("2026-06-25T11:59:59+00:00", body.GetProperty("lastReadAt").GetString());
        Assert.AreEqual(0, body.GetProperty("readErrorCount").GetInt64());
        Assert.IsFalse(body.TryGetProperty("lastError", out _));

        var roundTripped = AgentObservationJsonCodec.ReadSourceHealth(json);
        Assert.IsTrue(roundTripped.Body.CanRead);
    }

    [TestMethod]
    public void FilterSummaryObservation_SerializesPlannedRecordKindAndFields()
    {
        var observation = new FilterSummaryObservation(
            Metadata: WindowedMetadata() with { FilterId = "security-minimal" },
            Body: new FilterSummaryObservationBody(
                "WindowsEventLog",
                "Security",
                ReadCount: 5000,
                KeptAfterFilterCount: 1800,
                DiscardedCount: 3200,
                ForwardedCount: 1800));

        var json = AgentObservationJsonCodec.Write(observation);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var metadata = root.GetProperty("metadata");
        var body = root.GetProperty("body");

        Assert.AreEqual(AgentObservationRecordKinds.FilterSummary, root.GetProperty("recordKind").GetString());
        Assert.AreEqual("security-minimal", metadata.GetProperty("filterId").GetString());
        Assert.AreEqual("WindowsEventLog", body.GetProperty("sourceType").GetString());
        Assert.AreEqual("Security", body.GetProperty("channel").GetString());
        Assert.AreEqual(5000, body.GetProperty("readCount").GetInt64());
        Assert.AreEqual(1800, body.GetProperty("keptAfterFilterCount").GetInt64());
        Assert.AreEqual(3200, body.GetProperty("discardedCount").GetInt64());
        Assert.AreEqual(1800, body.GetProperty("forwardedCount").GetInt64());

        var roundTripped = AgentObservationJsonCodec.ReadFilterSummary(json);
        Assert.AreEqual("security-minimal", roundTripped.Metadata.FilterId);
    }


    [TestMethod]
    public void AgentObservationFixturePayloads_ParseWithStableRecordKinds()
    {
        var pipeline = AgentObservationJsonCodec.ReadPipelineCounts(ReadFixture("collector.pipeline.counts.json"));
        var health = AgentObservationJsonCodec.ReadSourceHealth(ReadFixture("collector.source.health.json"));
        var filter = AgentObservationJsonCodec.ReadFilterSummary(ReadFixture("collector.filter.summary.json"));

        Assert.AreEqual(AgentObservationRecordKinds.PipelineCounts, pipeline.RecordKind);
        Assert.AreEqual(AgentObservationRecordKinds.SourceHealth, health.RecordKind);
        Assert.AreEqual(AgentObservationRecordKinds.FilterSummary, filter.RecordKind);
        Assert.AreEqual("WindowsEventLog:Security:Microsoft-Windows-Security-Auditing:4688", pipeline.Body.LogKey.ToCanonicalString());
        Assert.AreEqual("security-minimal", filter.Metadata.FilterId);
    }

    [TestMethod]
    public void AgentObservationJsonCodec_RejectsUnexpectedRecordKind()
    {
        const string json = """
            {
              "recordKind": "collector.source.health",
              "metadata": {
                "agentId": "endpoint-123",
                "hostId": "host-abc",
                "profileId": "windows-security-default",
                "observedAt": "2026-06-25T12:00:00Z"
              },
              "body": {
                "sourceType": "WindowsEventLog",
                "channel": "Security",
                "isEnabled": true,
                "canRead": true,
                "readErrorCount": 0
              }
            }
            """;

        Assert.ThrowsExactly<FormatException>(() => AgentObservationJsonCodec.ReadPipelineCounts(json));
    }


    private static string ReadFixture(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "tests", "DeltaZulu.Platform.Tests", "Analytics", "Ingestion", "Fixtures", fileName);
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate agent observation fixture '{fileName}'.", fileName);
    }

    private static AgentObservationMetadata WindowedMetadata() => new(
        "endpoint-123",
        "host-abc",
        "windows-security-default",
        new DateTimeOffset(2026, 06, 25, 12, 00, 00, TimeSpan.Zero),
        new DateTimeOffset(2026, 06, 25, 11, 55, 00, TimeSpan.Zero),
        new DateTimeOffset(2026, 06, 25, 12, 00, 00, TimeSpan.Zero));

    private static AgentObservationMetadata HealthMetadata() => new(
        "endpoint-123",
        "host-abc",
        "windows-security-default",
        new DateTimeOffset(2026, 06, 25, 12, 00, 00, TimeSpan.Zero));
}
