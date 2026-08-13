using DeltaZulu.Platform.Data.DuckDb.Ingestion;
using DeltaZulu.Platform.Domain.Analytics.Observability;

namespace DeltaZulu.Platform.Web.AgentManagement.Hosting;

/// <summary>
/// Bridges the application-layer source observation port onto the DuckDB lake
/// writer registered by the analytics module.
/// </summary>
public sealed class DuckDbSourceObservationSinkAdapter(DuckDbSourceObservationWriter writer)
    : ISourceObservationSink
{
    public Task AppendBatchAsync(IReadOnlyList<SourceObservationSnapshot> snapshots, CancellationToken ct = default) =>
        writer.AppendBatchAsync(snapshots, ct);
}
