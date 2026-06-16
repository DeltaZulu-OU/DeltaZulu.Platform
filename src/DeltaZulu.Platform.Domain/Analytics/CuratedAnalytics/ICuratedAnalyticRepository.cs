namespace DeltaZulu.Platform.Domain.Analytics.CuratedAnalytics;

public interface ICuratedAnalyticRepository
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CuratedAnalyticRecord>> ListAsync(CancellationToken cancellationToken = default);

    Task<PageResult<CuratedAnalyticRecord>> SearchAsync(
        string? searchText,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    Task<CuratedAnalyticRecord?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task SaveAsync(CuratedAnalyticRecord record, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task MarkRunAsync(string id, DateTime runAt, CancellationToken cancellationToken = default);
}