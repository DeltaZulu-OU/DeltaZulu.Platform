namespace DeltaZulu.Platform.Domain.Analytics.SavedQueries;

public interface ISavedQueryRepository
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedQueryRecord>> ListAsync(CancellationToken cancellationToken = default);

    Task<SavedQueryRecord?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task SaveAsync(SavedQueryRecord query, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task MarkRunAsync(string id, DateTime runAt, CancellationToken cancellationToken = default);
}