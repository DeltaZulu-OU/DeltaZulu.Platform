namespace DeltaZulu.Platform.Domain.Analytics.Observability;

/// <summary>
/// Append-only sink for per-source health observations reported by agents.
/// Implemented by the DuckDB lake writer adapter so application services can emit
/// observations without depending on a storage backend.
/// </summary>
public interface ISourceObservationSink
{
    Task AppendBatchAsync(IReadOnlyList<SourceObservationSnapshot> snapshots, CancellationToken ct = default);
}
