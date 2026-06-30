using Dapper;
using DeltaZulu.Platform.Domain.Analytics.Observability;

namespace DeltaZulu.Platform.Data.DuckDb.Analytics;

public sealed class DuckDbOperationalMetricsReader : IOperationalMetricsReader
{
    private readonly DuckDbConnectionFactory _connectionFactory;

    public DuckDbOperationalMetricsReader(DuckDbConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public OverviewMetricsSummary ReadOverviewSummary(string tenantId = "default")
    {
        var conn = _connectionFactory.GetConnection();
        var row = conn.QuerySingleOrDefault<OverviewRow>(
            "SELECT * FROM internal.OverviewSummary WHERE TenantId = $TenantId",
            new { TenantId = tenantId });

        return row is null
            ? new OverviewMetricsSummary(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, tenantId)
            : new OverviewMetricsSummary(
                row.AgentCount,
                row.OnlineAgentCount,
                row.DegradedAgentCount,
                row.StaleAgentCount,
                row.OfflineAgentCount,
                row.ConfigDriftCount,
                row.SourceCount,
                row.HealthySourceCount,
                row.DegradedSourceCount,
                row.InactiveSourceCount,
                row.DisabledSourceCount,
                row.TotalRead,
                row.TotalKept,
                row.TotalDiscarded,
                row.TotalForwarded,
                row.SourceForwardFailedCount,
                row.AgentForwardFailedCount,
                row.OverallDiscardRatio,
                row.MaxBufferPressure,
                row.TenantId);
    }

    public AgentHealthSummary ReadAgentHealthSummary(string tenantId = "default")
    {
        var conn = _connectionFactory.GetConnection();
        var row = conn.QuerySingleOrDefault<AgentSummaryRow>(
            "SELECT * FROM internal.AgentHealthSummary WHERE TenantId = $TenantId",
            new { TenantId = tenantId });

        return row is null
            ? new AgentHealthSummary(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, tenantId)
            : new AgentHealthSummary(
                row.AgentCount,
                row.OnlineCount,
                row.StaleCount,
                row.OfflineCount,
                row.DegradedCount,
                row.DisabledCount,
                row.ConfigDriftCount,
                row.TotalQueueDepth,
                row.TotalDropped,
                row.TotalForwardFailed,
                row.MaxBufferPressure,
                row.TenantId);
    }

    public SourceHealthSummary ReadSourceHealthSummary(string tenantId = "default")
    {
        var conn = _connectionFactory.GetConnection();
        var row = conn.QuerySingleOrDefault<SourceSummaryRow>(
            "SELECT * FROM internal.SourceHealthSummary WHERE TenantId = $TenantId",
            new { TenantId = tenantId });

        return row is null
            ? new SourceHealthSummary(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, tenantId)
            : new SourceHealthSummary(
                row.SourceCount,
                row.AgentCount,
                row.HealthyCount,
                row.DegradedCount,
                row.DisabledCount,
                row.InactiveCount,
                row.TotalForwarded,
                row.TotalDiscarded,
                row.TotalForwardFailed,
                row.TotalRead,
                row.TotalKept,
                row.OverallDiscardRatio,
                row.TenantId);
    }

    public IReadOnlyList<SourceLatestRow> ReadLatestSources(string tenantId = "default", string? healthStatusFilter = null)
    {
        var conn = _connectionFactory.GetConnection();
        var sql = string.IsNullOrWhiteSpace(healthStatusFilter)
            ? "SELECT * FROM internal.SourceLatest WHERE TenantId = $TenantId ORDER BY SourceType, Channel, AgentId"
            : "SELECT * FROM internal.SourceLatest WHERE TenantId = $TenantId AND HealthStatus = $Status ORDER BY SourceType, Channel, AgentId";
        var param = new { TenantId = tenantId, Status = healthStatusFilter };

        return conn.Query<SourceLatestSqlRow>(sql, param).Select(MapSourceLatest).ToArray();
    }

    public IReadOnlyList<AgentLatestRow> ReadLatestAgents(string tenantId = "default", string? healthStatusFilter = null)
    {
        var conn = _connectionFactory.GetConnection();
        var sql = string.IsNullOrWhiteSpace(healthStatusFilter)
            ? "SELECT * FROM internal.AgentLatest WHERE TenantId = $TenantId ORDER BY Hostname, AgentId"
            : "SELECT * FROM internal.AgentLatest WHERE TenantId = $TenantId AND HealthStatus = $Status ORDER BY Hostname, AgentId";
        var param = new { TenantId = tenantId, Status = healthStatusFilter };

        return conn.Query<AgentLatestSqlRow>(sql, param)
            .Select(static r => new AgentLatestRow(
                r.ObservedAt,
                r.TenantId,
                r.AgentId,
                r.HostId,
                r.Hostname,
                r.Platform,
                r.AgentVersion,
                r.IsEnabled,
                r.ReportedStatus,
                r.HealthStatus,
                r.ConnectivityStatus,
                r.PipelineStatus,
                r.LastSeenAt,
                r.BufferPressure,
                r.QueueDepth,
                r.DroppedCount,
                r.ForwardFailedCount,
                r.ConfigDriftStatus,
                r.ConfigDrift,
                r.DesiredConfigVersionId,
                r.AppliedConfigVersionId,
                r.DesiredProfileVersionId,
                r.AppliedProfileVersionId))
            .ToArray();
    }

    public IReadOnlyList<CollectionCoverageRow> ReadObservedCollectionCoverage(string tenantId = "default", string? healthStatusFilter = null)
    {
        var conn = _connectionFactory.GetConnection();
        var sql = string.IsNullOrWhiteSpace(healthStatusFilter)
            ? "SELECT * FROM internal.ObservedCollectionCoverage WHERE TenantId = $TenantId ORDER BY Hostname, AgentId"
            : "SELECT * FROM internal.ObservedCollectionCoverage WHERE TenantId = $TenantId AND AgentHealthStatus = $Status ORDER BY Hostname, AgentId";
        var param = new { TenantId = tenantId, Status = healthStatusFilter };

        return conn.Query<CoverageSqlRow>(sql, param)
            .Select(static r => new CollectionCoverageRow(
                r.TenantId,
                r.AgentId,
                r.Hostname,
                r.Platform,
                r.AgentHealthStatus,
                r.SourceCount,
                r.HealthySourceCount,
                r.DegradedSourceCount,
                r.InactiveSourceCount,
                r.DisabledSourceCount,
                r.TotalForwarded,
                r.LatestAgentDroppedCount,
                r.SourceForwardFailedCount,
                r.AgentForwardFailedCount))
            .ToArray();
    }

    private static SourceLatestRow MapSourceLatest(SourceLatestSqlRow r) => new(
        r.ObservedAt,
        r.AgentId,
        r.HostId,
        r.SourceType,
        r.Channel,
        r.IsEnabled,
        r.CanRead,
        r.ReadErrorCount,
        r.ReadCount,
        r.KeptAfterFilterCount,
        r.DiscardedCount,
        r.ForwardedCount,
        r.ForwardFailedCount,
        r.HealthStatus,
        r.DiscardRatio,
        r.TenantId,
        r.SourceIdentity,
        r.SourceInstanceId,
        r.ResourceFamily,
        r.Provider,
        r.ProfileId,
        r.ProfileVersionId,
        r.LastReadAt,
        r.LastError);

    private sealed class OverviewRow
    {
        public string TenantId { get; init; } = string.Empty;
        public long AgentCount { get; init; }
        public long OnlineAgentCount { get; init; }
        public long DegradedAgentCount { get; init; }
        public long StaleAgentCount { get; init; }
        public long OfflineAgentCount { get; init; }
        public long ConfigDriftCount { get; init; }
        public long SourceCount { get; init; }
        public long HealthySourceCount { get; init; }
        public long DegradedSourceCount { get; init; }
        public long InactiveSourceCount { get; init; }
        public long DisabledSourceCount { get; init; }
        public long TotalRead { get; init; }
        public long TotalKept { get; init; }
        public long TotalDiscarded { get; init; }
        public long TotalForwarded { get; init; }
        public long SourceForwardFailedCount { get; init; }
        public long AgentForwardFailedCount { get; init; }
        public double OverallDiscardRatio { get; init; }
        public double MaxBufferPressure { get; init; }
    }

    private sealed class AgentSummaryRow
    {
        public string TenantId { get; init; } = string.Empty;
        public long AgentCount { get; init; }
        public long OnlineCount { get; init; }
        public long StaleCount { get; init; }
        public long OfflineCount { get; init; }
        public long DegradedCount { get; init; }
        public long DisabledCount { get; init; }
        public long ConfigDriftCount { get; init; }
        public long TotalQueueDepth { get; init; }
        public long TotalDropped { get; init; }
        public long TotalForwardFailed { get; init; }
        public double MaxBufferPressure { get; init; }
    }

    private sealed class SourceSummaryRow
    {
        public string TenantId { get; init; } = string.Empty;
        public long SourceCount { get; init; }
        public long AgentCount { get; init; }
        public long HealthyCount { get; init; }
        public long DegradedCount { get; init; }
        public long DisabledCount { get; init; }
        public long InactiveCount { get; init; }
        public long TotalForwarded { get; init; }
        public long TotalDiscarded { get; init; }
        public long TotalForwardFailed { get; init; }
        public long TotalRead { get; init; }
        public long TotalKept { get; init; }
        public double OverallDiscardRatio { get; init; }
    }

    private sealed class SourceLatestSqlRow
    {
        public DateTime ObservedAt { get; init; }
        public string TenantId { get; init; } = string.Empty;
        public string AgentId { get; init; } = string.Empty;
        public string HostId { get; init; } = string.Empty;
        public string SourceIdentity { get; init; } = string.Empty;
        public string? SourceInstanceId { get; init; }
        public string SourceType { get; init; } = string.Empty;
        public string? ResourceFamily { get; init; }
        public string? Provider { get; init; }
        public string Channel { get; init; } = string.Empty;
        public string? ProfileId { get; init; }
        public string? ProfileVersionId { get; init; }
        public bool IsEnabled { get; init; }
        public bool CanRead { get; init; }
        public DateTime? LastReadAt { get; init; }
        public long ReadErrorCount { get; init; }
        public string? LastError { get; init; }
        public long ReadCount { get; init; }
        public long KeptAfterFilterCount { get; init; }
        public long DiscardedCount { get; init; }
        public long ForwardedCount { get; init; }
        public long ForwardFailedCount { get; init; }
        public string HealthStatus { get; init; } = string.Empty;
        public double DiscardRatio { get; init; }
    }

    private sealed class AgentLatestSqlRow
    {
        public DateTime ObservedAt { get; init; }
        public string TenantId { get; init; } = string.Empty;
        public string AgentId { get; init; } = string.Empty;
        public string HostId { get; init; } = string.Empty;
        public string Hostname { get; init; } = string.Empty;
        public string Platform { get; init; } = string.Empty;
        public string AgentVersion { get; init; } = string.Empty;
        public DateTime? LastSeenAt { get; init; }
        public bool IsEnabled { get; init; }
        public string ReportedStatus { get; init; } = string.Empty;
        public double BufferPressure { get; init; }
        public long QueueDepth { get; init; }
        public long DroppedCount { get; init; }
        public long ForwardFailedCount { get; init; }
        public string? DesiredConfigVersionId { get; init; }
        public string? AppliedConfigVersionId { get; init; }
        public string? DesiredProfileVersionId { get; init; }
        public string? AppliedProfileVersionId { get; init; }
        public string ConfigDriftStatus { get; init; } = string.Empty;
        public bool ConfigDrift { get; init; }
        public string ConnectivityStatus { get; init; } = string.Empty;
        public string PipelineStatus { get; init; } = string.Empty;
        public string HealthStatus { get; init; } = string.Empty;
    }

    private sealed class CoverageSqlRow
    {
        public string TenantId { get; init; } = string.Empty;
        public string AgentId { get; init; } = string.Empty;
        public string Hostname { get; init; } = string.Empty;
        public string Platform { get; init; } = string.Empty;
        public string AgentHealthStatus { get; init; } = string.Empty;
        public long SourceCount { get; init; }
        public long HealthySourceCount { get; init; }
        public long DegradedSourceCount { get; init; }
        public long InactiveSourceCount { get; init; }
        public long DisabledSourceCount { get; init; }
        public long TotalForwarded { get; init; }
        public long LatestAgentDroppedCount { get; init; }
        public long SourceForwardFailedCount { get; init; }
        public long AgentForwardFailedCount { get; init; }
    }
}
