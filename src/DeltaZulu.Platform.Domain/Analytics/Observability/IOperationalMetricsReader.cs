namespace DeltaZulu.Platform.Domain.Analytics.Observability;

public interface IOperationalMetricsReader
{
    OverviewMetricsSummary ReadOverviewSummary(string tenantId = "default");

    AgentHealthSummary ReadAgentHealthSummary(string tenantId = "default");

    SourceHealthSummary ReadSourceHealthSummary(string tenantId = "default");

    IReadOnlyList<SourceLatestRow> ReadLatestSources(string tenantId = "default", string? healthStatusFilter = null);

    IReadOnlyList<AgentLatestRow> ReadLatestAgents(string tenantId = "default", string? healthStatusFilter = null);

    IReadOnlyList<CollectionCoverageRow> ReadObservedCollectionCoverage(string tenantId = "default", string? healthStatusFilter = null);

    /// <summary>Per-source utilization ranked by wasted volume (most discarded events first).</summary>
    IReadOnlyList<SourceUtilizationRow> ReadTopWastefulSources(string tenantId = "default", int limit = 10);

    /// <summary>Utilization rolled up by resource profile across every agent using it.</summary>
    IReadOnlyList<ProfileUtilizationRow> ReadProfileUtilization(string tenantId = "default");

    /// <summary>Agent-level utilization: read/forward/discard totals and buffer drop ratio.</summary>
    IReadOnlyList<AgentUtilizationRow> ReadAgentUtilization(string tenantId = "default");
}
