namespace DeltaZulu.Platform.Domain.Analytics.SavedQueries;

public interface ISavedQueryRepository
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedQueryRecord>> ListAsync(CancellationToken cancellationToken = default);

    async Task<SavedQueryPage> SearchAsync(
        string? searchText,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        var normalizedSearch = string.IsNullOrWhiteSpace(searchText) ? null : searchText.Trim();
        var records = await ListAsync(cancellationToken);
        var filtered = normalizedSearch is null
            ? records
            : records
                .Where(record => record.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(record.Description)
                        && record.Description.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

        return new SavedQueryPage(
            filtered.Skip(offset).Take(limit).ToArray(),
            filtered.Count,
            offset,
            limit);
    }

    Task<SavedQueryRecord?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task SaveAsync(SavedQueryRecord query, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task MarkRunAsync(string id, DateTime runAt, CancellationToken cancellationToken = default);
}
