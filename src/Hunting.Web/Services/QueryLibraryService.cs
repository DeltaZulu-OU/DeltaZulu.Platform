namespace Hunting.Web.Services;

using Hunting.Application.QueryHistory;
using Hunting.Application.SavedQueries;

/// <summary>
/// Application-facing service for saved queries and recent query history.
/// This keeps editor components from depending directly on persistence repositories.
/// </summary>
public sealed class QueryLibraryService
{
    private const int DefaultRecentHistoryLimit = 25;

    private readonly IQueryHistoryRepository _queryHistory;
    private readonly ISavedQueryRepository _savedQueries;

    public QueryLibraryService(
        ISavedQueryRepository savedQueries,
        IQueryHistoryRepository queryHistory)
    {
        _savedQueries = savedQueries;
        _queryHistory = queryHistory;
    }

    public Task<IReadOnlyList<SavedQueryRecord>> ListSavedQueriesAsync(
        CancellationToken cancellationToken = default)
    {
        return _savedQueries.ListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<QueryHistoryRecord>> ListRecentHistoryAsync(
        int limit = DefaultRecentHistoryLimit,
        CancellationToken cancellationToken = default)
    {
        return _queryHistory.ListRecentAsync(limit, cancellationToken);
    }

    public async Task<SavedQueryRecord> SaveQueryAsync(
        string? id,
        string name,
        string? description,
        string queryText,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryText);

        var now = DateTime.UtcNow;
        var normalizedId = string.IsNullOrWhiteSpace(id)
            ? Guid.NewGuid().ToString("N")
            : id;

        var existing = await _savedQueries.GetAsync(normalizedId, cancellationToken);
        var record = new SavedQueryRecord(
            normalizedId,
            name.Trim(),
            NormalizeOptionalText(description),
            queryText,
            existing?.CreatedAt ?? now,
            now,
            existing?.LastRunAt);

        await _savedQueries.SaveAsync(record, cancellationToken);
        return record;
    }

    public Task DeleteSavedQueryAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return _savedQueries.DeleteAsync(id, cancellationToken);
    }

    public async Task<string?> LoadSavedQueryTextAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var query = await _savedQueries.GetAsync(id, cancellationToken);
        return query?.QueryText;
    }

    public async Task<string?> LoadHistoryQueryTextAsync(
        string historyId,
        CancellationToken cancellationToken = default)
    {
        var history = await _queryHistory.ListRecentAsync(100, cancellationToken);
        return history.FirstOrDefault(item => item.Id == historyId)?.QueryText;
    }

    public Task MarkSavedQueryRunAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return _savedQueries.MarkRunAsync(id, DateTime.UtcNow, cancellationToken);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
