using DeltaZulu.Platform.Data.DuckDb.Ingestion;
using DeltaZulu.Platform.Domain.Analytics.Observability;

namespace DeltaZulu.Platform.Web.AgentManagement.Hosting;

/// <summary>
/// Bridges the application-layer observation port onto the DuckDB lake writer
/// registered by the analytics module.
/// </summary>
public sealed class DuckDbAgentObservationSinkAdapter(DuckDbAgentObservationWriter writer)
    : IAgentObservationSink
{
    public Task AppendAsync(AgentObservationSnapshot snapshot, CancellationToken ct = default) =>
        writer.AppendAsync(snapshot, ct);
}
