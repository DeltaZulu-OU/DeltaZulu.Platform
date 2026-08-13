namespace DeltaZulu.Platform.Domain.Analytics.Observability;

public sealed record OverviewMetricsSummary(
    long AgentCount,
    long OnlineAgentCount,
    long DegradedAgentCount,
    long StaleAgentCount,
    long OfflineAgentCount,
    long ConfigDriftCount,
    long SourceCount,
    long HealthySourceCount,
    long DegradedSourceCount,
    long InactiveSourceCount,
    long DisabledSourceCount,
    long TotalRead,
    long TotalKept,
    long TotalDiscarded,
    long TotalForwarded,
    long SourceForwardFailedCount,
    long AgentForwardFailedCount,
    double OverallDiscardRatio,
    double MaxBufferPressure,
    string TenantId = "default");
