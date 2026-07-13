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
    long TotalRead,
    long TotalKept = 0,
    double OverallDiscardRatio = 0,
    string TenantId = "default",
    long TotalReadErrors = 0,
    double ForwardingYield = 0,
    double ForwardFailureRate = 0,
    double ReadErrorRate = 0);
