namespace DeltaZulu.Platform.Domain.Analytics.Observability;

/// <summary>
/// Raw observation emitted by an agent for one configured log source.
/// Health, coverage, and dashboard rollups are computed by DuckDB views.
/// </summary>
public sealed record SourceObservationSnapshot(
    string SourceType,
    string Channel,
    string AgentId,
    string HostId,
    bool IsEnabled,
    bool CanRead,
    DateTime? LastReadAtUtc,
    long ReadErrorCount,
    string? LastError,
    long ReadCount,
    long KeptAfterFilterCount,
    long DiscardedCount,
    long ForwardedCount,
    long ForwardFailedCount,
    DateTime ObservedAtUtc,
    string TenantId = "default",
    string? SourceInstanceId = null,
    string? ResourceFamily = null,
    string? Provider = null,
    string? ProfileId = null,
    string? ProfileVersionId = null,
    DateTime? WindowStartUtc = null,
    DateTime? WindowEndUtc = null)
{
    public SourceHealthStatus HealthStatus
    {
        get
        {
            if (!IsEnabled) return SourceHealthStatus.Disabled;
            if (!CanRead || ReadErrorCount > 0) return SourceHealthStatus.Degraded;
            if (ForwardFailedCount > 0) return SourceHealthStatus.Degraded;
            if (ReadCount == 0) return SourceHealthStatus.Inactive;
            return SourceHealthStatus.Healthy;
        }
    }

    public double DiscardRatio => ReadCount > 0
        ? (double)DiscardedCount / ReadCount
        : 0;
}

public enum SourceHealthStatus
{
    Healthy,
    Degraded,
    Inactive,
    Disabled
}
