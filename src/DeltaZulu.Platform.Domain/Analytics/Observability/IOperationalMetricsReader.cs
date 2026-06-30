namespace DeltaZulu.Platform.Domain.Analytics.Observability;

public interface IOperationalMetricsReader
{
    OverviewMetricsSummary ReadOverviewSummary(string tenantId = "default");

    AgentHealthSummary ReadAgentHealthSummary(string tenantId = "default");

    SourceHealthSummary ReadSourceHealthSummary(string tenantId = "default");

    IReadOnlyList<SourceLatestRow> ReadLatestSources(string tenantId = "default", string? healthStatusFilter = null);

    IReadOnlyList<AgentLatestRow> ReadLatestAgents(string tenantId = "default", string? healthStatusFilter = null);

    IReadOnlyList<CollectionCoverageRow> ReadObservedCollectionCoverage(string tenantId = "default", string? healthStatusFilter = null);
}
