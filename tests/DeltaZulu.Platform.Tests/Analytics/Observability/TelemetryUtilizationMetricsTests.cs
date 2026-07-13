using DeltaZulu.Platform.Data.DuckDb;
using DeltaZulu.Platform.Data.DuckDb.Analytics;
using DeltaZulu.Platform.Data.DuckDb.Ingestion;
using DeltaZulu.Platform.Data.DuckDb.Sql;
using DeltaZulu.Platform.Domain.Analytics.Observability;
using DeltaZulu.Platform.Domain.Analytics.Schema;

namespace DeltaZulu.Platform.Tests.Analytics.Observability;

/// <summary>
/// Covers the telemetry utilization surfaces added on top of SourceLatest/AgentLatest:
/// fleet-wide forwarding yield and failure rates, per-source waste ranking, per-profile
/// rollups, and the agent-level buffer drop ratio.
/// </summary>
[TestClass]
public sealed class TelemetryUtilizationMetricsTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    [TestMethod]
    public async Task SourceHealthSummary_ComputesFleetWideUtilizationRatios()
    {
        using var factory = new DuckDbConnectionFactory("DataSource=:memory:");
        var applier = ApplySchema(factory);
        var writer = new DuckDbSourceObservationWriter(applier);

        await writer.AppendBatchAsync([
            Source(agentId: "agent-1", channel: "Security", readCount: 100, keptCount: 80,
                discardedCount: 20, forwardedCount: 78, forwardFailedCount: 2, readErrorCount: 1,
                profileId: "profile-x"),
            Source(agentId: "agent-2", channel: "Application", readCount: 50, keptCount: 50,
                discardedCount: 0, forwardedCount: 50, forwardFailedCount: 0, readErrorCount: 0,
                profileId: "profile-x"),
        ]);

        var reader = new DuckDbOperationalMetricsReader(factory);
        var summary = reader.ReadSourceHealthSummary();

        Assert.AreEqual(150, summary.TotalRead);
        Assert.AreEqual(128, summary.TotalForwarded);
        Assert.AreEqual(20, summary.TotalDiscarded);
        Assert.AreEqual(1, summary.TotalReadErrors);
        Assert.AreEqual(128.0 / 150, summary.ForwardingYield, 1e-9);
        Assert.AreEqual(2.0 / 130, summary.ForwardFailureRate, 1e-9);
        Assert.AreEqual(1.0 / 150, summary.ReadErrorRate, 1e-9);
    }

    [TestMethod]
    public async Task ReadTopWastefulSources_RanksByDiscardedVolume_NotRatio()
    {
        using var factory = new DuckDbConnectionFactory("DataSource=:memory:");
        var applier = ApplySchema(factory);
        var writer = new DuckDbSourceObservationWriter(applier);

        await writer.AppendBatchAsync([
            // High volume, moderate waste — should rank first by absolute discarded count.
            Source(agentId: "agent-1", channel: "Security", readCount: 1000, keptCount: 800,
                discardedCount: 200, forwardedCount: 800, forwardFailedCount: 0, readErrorCount: 0),
            // Low volume, near-total waste — smaller absolute cost despite a worse ratio.
            Source(agentId: "agent-2", channel: "Noisy", readCount: 10, keptCount: 1,
                discardedCount: 9, forwardedCount: 1, forwardFailedCount: 0, readErrorCount: 0),
            // No waste at all.
            Source(agentId: "agent-3", channel: "Clean", readCount: 500, keptCount: 500,
                discardedCount: 0, forwardedCount: 500, forwardFailedCount: 0, readErrorCount: 0),
        ]);

        var reader = new DuckDbOperationalMetricsReader(factory);
        var top = reader.ReadTopWastefulSources(limit: 2);

        Assert.HasCount(2, top);
        Assert.AreEqual("Security", top[0].Channel);
        Assert.AreEqual(200, top[0].DiscardedCount);
        Assert.AreEqual("Noisy", top[1].Channel);
        Assert.AreEqual(0.8, top[0].ForwardingYield, 1e-9);
    }

    [TestMethod]
    public async Task ReadProfileUtilization_RollsUpAcrossAgents_AndGroupsUnassigned()
    {
        using var factory = new DuckDbConnectionFactory("DataSource=:memory:");
        var applier = ApplySchema(factory);
        var writer = new DuckDbSourceObservationWriter(applier);

        await writer.AppendBatchAsync([
            Source(agentId: "agent-1", channel: "Security", readCount: 100, keptCount: 80,
                discardedCount: 20, forwardedCount: 78, forwardFailedCount: 2, readErrorCount: 1,
                profileId: "profile-x"),
            Source(agentId: "agent-2", channel: "Application", readCount: 50, keptCount: 50,
                discardedCount: 0, forwardedCount: 50, forwardFailedCount: 0, readErrorCount: 0,
                profileId: "profile-x"),
            Source(agentId: "agent-1", channel: "System", readCount: 10, keptCount: 2,
                discardedCount: 8, forwardedCount: 1, forwardFailedCount: 1, readErrorCount: 0,
                profileId: null),
        ]);

        var reader = new DuckDbOperationalMetricsReader(factory);
        var rollups = reader.ReadProfileUtilization().ToDictionary(r => r.ProfileId);

        Assert.HasCount(2, rollups);

        var profileX = rollups["profile-x"];
        Assert.AreEqual(2, profileX.SourceCount);
        Assert.AreEqual(2, profileX.AgentCount);
        Assert.AreEqual(150, profileX.TotalRead);
        Assert.AreEqual(128, profileX.TotalForwarded);
        Assert.AreEqual(20, profileX.TotalDiscarded);
        Assert.AreEqual(128.0 / 150, profileX.ForwardingYield, 1e-9);

        var unassigned = rollups[ProfileUtilizationRow.UnassignedProfileId];
        Assert.AreEqual(1, unassigned.SourceCount);
        Assert.AreEqual(10, unassigned.TotalRead);
        Assert.AreEqual(8, unassigned.TotalDiscarded);
        Assert.AreEqual(0.5, unassigned.ForwardFailureRate, 1e-9);
    }

    [TestMethod]
    public async Task ReadAgentUtilization_ComputesBufferDropRatio_DistinctFromDiscardRatio()
    {
        using var factory = new DuckDbConnectionFactory("DataSource=:memory:");
        var applier = ApplySchema(factory);
        var sourceWriter = new DuckDbSourceObservationWriter(applier);
        var agentWriter = new DuckDbAgentObservationWriter(applier);

        await agentWriter.AppendAsync(new AgentObservationSnapshot(
            TenantId: "default", AgentId: "agent-1", HostId: "host-1", Hostname: "host-1",
            Platform: "Windows", AgentVersion: "1.0.0", ObservedAtUtc: Now, LastSeenAtUtc: Now,
            IsEnabled: true, ReportedStatus: "Running", BufferPressure: 0.5,
            QueueDepth: 10, DroppedCount: 5, ForwardFailedCount: 0));
        await agentWriter.AppendAsync(new AgentObservationSnapshot(
            TenantId: "default", AgentId: "agent-2", HostId: "host-2", Hostname: "host-2",
            Platform: "Linux", AgentVersion: "1.0.0", ObservedAtUtc: Now, LastSeenAtUtc: Now,
            IsEnabled: true, ReportedStatus: "Running", BufferPressure: 0.1,
            QueueDepth: 0, DroppedCount: 0, ForwardFailedCount: 0));

        await sourceWriter.AppendBatchAsync([
            Source(agentId: "agent-1", channel: "Security", readCount: 100, keptCount: 80,
                discardedCount: 20, forwardedCount: 78, forwardFailedCount: 0, readErrorCount: 0),
            Source(agentId: "agent-1", channel: "System", readCount: 10, keptCount: 2,
                discardedCount: 8, forwardedCount: 1, forwardFailedCount: 0, readErrorCount: 0),
            Source(agentId: "agent-2", channel: "Application", readCount: 50, keptCount: 50,
                discardedCount: 0, forwardedCount: 50, forwardFailedCount: 0, readErrorCount: 0),
        ]);

        var reader = new DuckDbOperationalMetricsReader(factory);
        var rows = reader.ReadAgentUtilization().ToDictionary(r => r.AgentId);

        var agent1 = rows["agent-1"];
        Assert.AreEqual(5, agent1.DroppedCount);
        Assert.AreEqual(79, agent1.TotalForwarded);
        Assert.AreEqual(5.0 / 84, agent1.BufferDropRatio, 1e-9);

        var agent2 = rows["agent-2"];
        Assert.AreEqual(0, agent2.DroppedCount);
        Assert.AreEqual(0, agent2.BufferDropRatio);
    }

    private static SchemaApplier ApplySchema(DuckDbConnectionFactory factory)
    {
        var applier = new SchemaApplier(factory);
        var emitter = new SchemaEmitter();
        applier.ApplyStatements(emitter.EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: SchemaConventions.InternalTables,
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews,
            internalViews: SchemaConventions.InternalViews));
        return applier;
    }

    private static SourceObservationSnapshot Source(
        string agentId,
        string channel,
        long readCount,
        long keptCount,
        long discardedCount,
        long forwardedCount,
        long forwardFailedCount,
        long readErrorCount,
        string? profileId = null) =>
        new(
            SourceType: "WindowsEventLog",
            Channel: channel,
            AgentId: agentId,
            HostId: $"{agentId}-host",
            IsEnabled: true,
            CanRead: true,
            LastReadAtUtc: Now,
            ReadErrorCount: readErrorCount,
            LastError: null,
            ReadCount: readCount,
            KeptAfterFilterCount: keptCount,
            DiscardedCount: discardedCount,
            ForwardedCount: forwardedCount,
            ForwardFailedCount: forwardFailedCount,
            ObservedAtUtc: Now,
            TenantId: "default",
            ProfileId: profileId);
}
