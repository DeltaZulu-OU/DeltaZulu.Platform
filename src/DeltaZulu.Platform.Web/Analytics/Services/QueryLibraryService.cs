
using DeltaZulu.Platform.Domain.Analytics.QueryHistory;
using DeltaZulu.Platform.Domain.Analytics.SavedQueries;
using DeltaZulu.Platform.Domain.Analytics.Visualizations;

namespace DeltaZulu.Platform.Web.Analytics.Services;
/// <summary>
/// Application-facing service for saved queries and recent query history.
/// This keeps editor components from depending directly on persistence repositories.
/// </summary>
public sealed class QueryLibraryService
{
    private const int DefaultRecentHistoryLimit = 25;

    private readonly IQueryHistoryRepository _queryHistory;
    private readonly ISavedQueryRepository _savedQueries;
    private readonly IVisualizationRepository _visualizations;

    public QueryLibraryService(
        ISavedQueryRepository savedQueries,
        IQueryHistoryRepository queryHistory,
        IVisualizationRepository visualizations)
    {
        _savedQueries = savedQueries ?? throw new ArgumentNullException(nameof(savedQueries));
        _queryHistory = queryHistory ?? throw new ArgumentNullException(nameof(queryHistory));
        _visualizations = visualizations ?? throw new ArgumentNullException(nameof(visualizations));
    }

    public Task<IReadOnlyList<SavedQueryRecord>> ListSavedQueriesAsync(
        CancellationToken cancellationToken = default) => _savedQueries.ListAsync(cancellationToken);

    public Task<SavedQueryPage> SearchSavedQueriesAsync(
        string? searchText,
        int offset,
        int limit,
        CancellationToken cancellationToken = default) => _savedQueries.SearchAsync(searchText, offset, limit, cancellationToken);

    public Task<IReadOnlyList<QueryHistoryRecord>> ListRecentHistoryAsync(
        int limit = DefaultRecentHistoryLimit,
        CancellationToken cancellationToken = default) => _queryHistory.ListRecentAsync(limit, cancellationToken);

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
            : id.Trim();

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

    public async Task DeleteSavedQueryAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var visualizations = await _visualizations.ListByQueryAsync(id.Trim(), cancellationToken);
        if (visualizations.Count > 0)
        {
            throw new InvalidOperationException(CreateInUseDeleteMessage(id, visualizations));
        }

        await _savedQueries.DeleteAsync(id, cancellationToken);
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
        CancellationToken cancellationToken = default) => _savedQueries.MarkRunAsync(id, DateTime.UtcNow, cancellationToken);

    private static string CreateInUseDeleteMessage(
        string queryId,
        IReadOnlyList<VisualizationRecord> visualizations)
    {
        var names = visualizations
            .Select(visualization => string.IsNullOrWhiteSpace(visualization.Name) ? visualization.Id : visualization.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();

        var suffix = visualizations.Count > names.Length ? "…" : string.Empty;
        return $"Saved query '{queryId}' is used by {visualizations.Count} saved visualization(s): {string.Join(", ", names)}{suffix}. Delete or reassign those visualizations before deleting the query.";
    }

    private static string? NormalizeOptionalText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}