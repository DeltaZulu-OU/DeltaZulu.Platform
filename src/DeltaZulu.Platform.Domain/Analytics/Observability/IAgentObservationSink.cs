namespace DeltaZulu.Platform.Domain.Analytics.Observability;

/// <summary>
/// Append-only sink for agent runtime observations. Implemented by the DuckDB lake
/// writer adapter so application services can emit observations without depending
/// on a storage backend.
/// </summary>
public interface IAgentObservationSink
{
    Task AppendAsync(AgentObservationSnapshot snapshot, CancellationToken ct = default);
}
