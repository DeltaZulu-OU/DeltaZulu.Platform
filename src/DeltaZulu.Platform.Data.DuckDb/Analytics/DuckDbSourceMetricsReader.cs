using DeltaZulu.Platform.Domain.Analytics.Observability;

namespace DeltaZulu.Platform.Data.DuckDb.Analytics;

public sealed class DuckDbSourceMetricsReader : ISourceMetricsReader
{
    private readonly IOperationalMetricsReader _operationalMetricsReader;

    public DuckDbSourceMetricsReader(DuckDbConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _operationalMetricsReader = new DuckDbOperationalMetricsReader(connectionFactory);
    }

    public SourceHealthSummary ReadSummary() =>
        _operationalMetricsReader.ReadSourceHealthSummary();

    public IReadOnlyList<SourceLatestRow> ReadLatest(string? healthStatusFilter = null) =>
        _operationalMetricsReader.ReadLatestSources(healthStatusFilter: healthStatusFilter);
}
