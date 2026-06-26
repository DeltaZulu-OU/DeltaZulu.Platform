namespace DeltaZulu.Platform.Ingestion.Observability;

/// <summary>
/// Stable record-kind discriminators for collector operational telemetry emitted by endpoint agents.
/// </summary>
public static class AgentObservationRecordKinds
{
    public const string PipelineCounts = "collector.pipeline.counts";
    public const string SourceHealth = "collector.source.health";
    public const string FilterSummary = "collector.filter.summary";
}
