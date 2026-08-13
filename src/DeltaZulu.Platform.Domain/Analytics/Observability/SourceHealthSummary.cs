namespace DeltaZulu.Platform.Domain.Analytics.Observability;

public sealed record SourceHealthSummary(
    long SourceCount,
    long AgentCount,
    long HealthyCount,
    long DegradedCount,
    long DisabledCount,
    long InactiveCount,
    long TotalForwarded,
    long TotalDiscarded,
    long TotalForwardFailed,
    long TotalRead);
