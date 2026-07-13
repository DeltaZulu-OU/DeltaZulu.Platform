namespace DeltaZulu.Platform.Domain.Analytics.Observability;

/// <summary>
/// Per-source utilization: how much of what was read actually left the box, and
/// at what cost in errors/failures. Extends the identity fields from SourceLatest
/// with ratios used to prioritize filter/policy tuning.
/// </summary>
public sealed record SourceUtilizationRow(
    string TenantId,
    string AgentId,
    string HostId,
    string SourceIdentity,
    string SourceType,
    string Channel,
    string? ProfileId,
    string? ProfileVersionId,
    long ReadCount,
    long KeptAfterFilterCount,
    long DiscardedCount,
    long ForwardedCount,
    long ForwardFailedCount,
    long ReadErrorCount,
    string HealthStatus,
    double DiscardRatio,
    double ForwardingYield,
    double ForwardFailureRate,
    double ReadErrorRate,
    DateTime ObservedAt,
    DateTime? LastReadAt);
