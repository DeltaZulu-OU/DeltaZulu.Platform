namespace DeltaZulu.Hunting.Application.QueryHistory;

public interface IQueryHistoryRepository
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QueryHistoryRecord>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default);
    Task AddAsync(QueryHistoryRecord record, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
