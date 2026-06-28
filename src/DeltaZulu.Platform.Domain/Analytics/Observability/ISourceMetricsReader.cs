namespace DeltaZulu.Platform.Domain.Analytics.Observability;

public interface ISourceMetricsReader
{
    SourceHealthSummary ReadSummary();

    IReadOnlyList<SourceLatestRow> ReadLatest(string? healthStatusFilter = null);
}
