namespace DeltaZulu.Platform.Domain.Hunting.Visualizations;

public interface IVisualizationRepository
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VisualizationRecord>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VisualizationRecord>> ListByQueryAsync(string queryId, CancellationToken cancellationToken = default);

    Task<VisualizationRecord?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task SaveAsync(VisualizationRecord visualization, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}