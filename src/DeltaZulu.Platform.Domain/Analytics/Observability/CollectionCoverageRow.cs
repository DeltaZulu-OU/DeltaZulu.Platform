namespace DeltaZulu.Platform.Domain.Analytics.Observability;

public sealed record CollectionCoverageRow(
    string TenantId,
    string AgentId,
    string Hostname,
    string Platform,
    string AgentHealthStatus,
    long SourceCount,
    long HealthySourceCount,
    long DegradedSourceCount,
    long InactiveSourceCount,
    long DisabledSourceCount,
    long TotalForwarded,
    long LatestAgentDroppedCount,
    long SourceForwardFailedCount,
    long AgentForwardFailedCount);
