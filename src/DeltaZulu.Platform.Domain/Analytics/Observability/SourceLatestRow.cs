namespace DeltaZulu.Platform.Domain.Analytics.Observability;

public sealed record SourceLatestRow(
    DateTime ObservedAt,
    string AgentId,
    string HostId,
    string SourceType,
    string Channel,
    bool IsEnabled,
    bool CanRead,
    long ReadErrorCount,
    long ReadCount,
    long KeptAfterFilterCount,
    long DiscardedCount,
    long ForwardedCount,
    long ForwardFailedCount,
    string HealthStatus,
    double DiscardRatio);
