namespace DeltaZulu.Platform.Domain.Analytics.Observability;

public sealed record AgentHealthSummary(
    long AgentCount,
    long OnlineCount,
    long StaleCount,
    long OfflineCount,
    long DegradedCount,
    long DisabledCount,
    long ConfigDriftCount,
    long TotalQueueDepth,
    long TotalDropped,
    long TotalForwardFailed,
    double MaxBufferPressure,
    string TenantId = "default");
