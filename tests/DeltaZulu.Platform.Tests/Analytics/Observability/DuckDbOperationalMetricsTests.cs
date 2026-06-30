using DeltaZulu.Platform.Data.DuckDb;
using DeltaZulu.Platform.Data.DuckDb.Analytics;
using DeltaZulu.Platform.Data.DuckDb.Ingestion;
using DeltaZulu.Platform.Data.DuckDb.Sql;
using DeltaZulu.Platform.Domain.Analytics.Observability;
using DeltaZulu.Platform.Domain.Analytics.Schema;

namespace DeltaZulu.Platform.Tests.Analytics.Observability;

[TestClass]
public sealed class DuckDbOperationalMetricsTests
{
    [TestMethod]
    public async Task OverviewSummary_IsTenantScoped_AndCountsSourceInstances()
    {
        using var factory = new DuckDbConnectionFactory("DataSource=:memory:");
        var applier = ApplySchema(factory);
        var sourceWriter = new DuckDbSourceObservationWriter(applier);
        var agentWriter = new DuckDbAgentObservationWriter(applier);
        var now = DateTime.UtcNow;

        await agentWriter.AppendAsync(new AgentObservationSnapshot(
            "tenant-a", "agent-1", "host-1", "host-1", "Windows", "1.0.0",
            now, now, true, "Running", 0.10, 2, 0, 0,
            "cfg-1", "cfg-1", "profile-1", "profile-1"));
        await agentWriter.AppendAsync(new AgentObservationSnapshot(
            "tenant-b", "agent-1", "host-1", "host-1", "Windows", "1.0.0",
            now, now, true, "Running", 0.90, 4, 0, 1,
            "cfg-1", "cfg-2", "profile-1", "profile-1"));

        await sourceWriter.AppendBatchAsync([
            Source("tenant-a", "agent-1", "Security", 100, 10),
            Source("tenant-a", "agent-2", "Security", 50, 5),
            Source("tenant-b", "agent-1", "Security", 25, 0)
        ]);

        var reader = new DuckDbOperationalMetricsReader(factory);
        var tenantA = reader.ReadOverviewSummary("tenant-a");
        var tenantB = reader.ReadOverviewSummary("tenant-b");

        Assert.AreEqual(1, tenantA.AgentCount);
        Assert.AreEqual(2, tenantA.SourceCount);
        Assert.AreEqual(150, tenantA.TotalRead);
        Assert.AreEqual(0, tenantA.ConfigDriftCount);

        Assert.AreEqual(1, tenantB.AgentCount);
        Assert.AreEqual(1, tenantB.SourceCount);
        Assert.AreEqual(25, tenantB.TotalRead);
        Assert.AreEqual(1, tenantB.ConfigDriftCount);
        Assert.AreEqual(1, tenantB.AgentForwardFailedCount);
    }

    [TestMethod]
    public async Task AgentLatest_PrioritizesStaleOverPipelineDegraded()
    {
        using var factory = new DuckDbConnectionFactory("DataSource=:memory:");
        var applier = ApplySchema(factory);
        var writer = new DuckDbAgentObservationWriter(applier);
        var now = DateTime.UtcNow;

        await writer.AppendAsync(new AgentObservationSnapshot(
            "default", "agent-stale", "host-stale", "host-stale", "Windows", "1.0.0",
            now, now.AddHours(-1), true, "Running", 0.95, 10, 3, 2,
            "cfg-1", "cfg-1", "profile-1", "profile-1"));

        var reader = new DuckDbOperationalMetricsReader(factory);
        var row = reader.ReadLatestAgents().Single();

        Assert.AreEqual("Stale", row.HealthStatus);
        Assert.AreEqual("Stale", row.ConnectivityStatus);
        Assert.AreEqual("Degraded", row.PipelineStatus);
    }

    [TestMethod]
    public async Task SourceLatest_UsesBlankSourceInstanceFallback()
    {
        using var factory = new DuckDbConnectionFactory("DataSource=:memory:");
        var applier = ApplySchema(factory);
        var writer = new DuckDbSourceObservationWriter(applier);

        await writer.AppendBatchAsync([
            Source("default", "agent-1", "Security", 100, 10, sourceInstanceId: ""),
            Source("default", "agent-1", "Application", 50, 0, sourceInstanceId: "")
        ]);

        var reader = new DuckDbOperationalMetricsReader(factory);
        var rows = reader.ReadLatestSources();

        Assert.AreEqual(2, rows.Count);
        Assert.Contains("WindowsEventLog:Security", rows.Select(static r => r.SourceIdentity).ToArray());
        Assert.Contains("WindowsEventLog:Application", rows.Select(static r => r.SourceIdentity).ToArray());
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
        string tenantId,
        string agentId,
        string channel,
        long readCount,
        long discardedCount,
        string? sourceInstanceId = null) =>
        new("WindowsEventLog", channel, agentId, $"{agentId}-host",
            IsEnabled: true, CanRead: true, LastReadAtUtc: DateTime.UtcNow,
            ReadErrorCount: 0, LastError: null,
            ReadCount: readCount, KeptAfterFilterCount: readCount - discardedCount,
            DiscardedCount: discardedCount, ForwardedCount: readCount - discardedCount,
            ForwardFailedCount: 0, ObservedAtUtc: DateTime.UtcNow,
            TenantId: tenantId, SourceInstanceId: sourceInstanceId,
            ResourceFamily: "EventLog", Provider: "Microsoft-Windows-Eventlog");
}
