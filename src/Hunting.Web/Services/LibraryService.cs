namespace Hunting.Web.Services;

using Hunting.Application.SavedQueries;
using Hunting.Application.Visualizations;
using Hunting.Web.Dashboards.Persistence;

/// <summary>
/// Web-facing aggregate over saved queries, visualizations, and dashboards.
/// This is intentionally a UI/application composition service; it does not move
/// persistence responsibilities or create new project boundaries.
/// </summary>
public sealed class LibraryService
{
    private readonly IDashboardRepository _dashboards;
    private readonly QueryLibraryService _queries;
    private readonly VisualizationLibraryService _visualizations;

    public LibraryService(
        QueryLibraryService queries,
        VisualizationLibraryService visualizations,
        IDashboardRepository dashboards)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _visualizations = visualizations ?? throw new ArgumentNullException(nameof(visualizations));
        _dashboards = dashboards ?? throw new ArgumentNullException(nameof(dashboards));
    }

    public async Task<IReadOnlyList<LibraryItem>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var queries = await _queries.ListSavedQueriesAsync(cancellationToken);
        var visualizations = await _visualizations.ListVisualizationsAsync(cancellationToken);
        var dashboards = await _dashboards.ListAsync(cancellationToken);

        var queryNames = queries.ToDictionary(
            query => query.Id,
            query => string.IsNullOrWhiteSpace(query.Name) ? query.Id : query.Name,
            StringComparer.OrdinalIgnoreCase);

        var visualizationCountsByQuery = visualizations
            .GroupBy(visualization => visualization.QueryId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Count(),
                StringComparer.OrdinalIgnoreCase);

        var items = new List<LibraryItem>(
            queries.Count + visualizations.Count + dashboards.Count);

        foreach (var query in queries)
        {
            visualizationCountsByQuery.TryGetValue(query.Id, out var visualizationCount);
            items.Add(new LibraryItem(
                query.Id,
                LibraryItemKind.SavedQuery,
                query.Name,
                query.Description,
                visualizationCount == 0
                    ? "No saved visualizations"
                    : $"{visualizationCount} saved visualization(s)",
                query.UpdatedAt,
                LibraryItemStatus.Ok));
        }

        foreach (var visualization in visualizations)
        {
            var hasQuery = queryNames.TryGetValue(visualization.QueryId, out var queryName);
            items.Add(new LibraryItem(
                visualization.Id,
                LibraryItemKind.Visualization,
                visualization.Name,
                visualization.Description,
                hasQuery
                    ? $"Query: {queryName}"
                    : $"Missing query: {ShortId(visualization.QueryId)}",
                visualization.UpdatedAt,
                hasQuery ? LibraryItemStatus.Ok : LibraryItemStatus.MissingDependency));
        }

        foreach (var dashboard in dashboards)
        {
            items.Add(new LibraryItem(
                dashboard.Id,
                LibraryItemKind.Dashboard,
                dashboard.Name,
                dashboard.Description,
                $"{dashboard.WidgetCount} widget(s)",
                dashboard.UpdatedAtUtc,
                LibraryItemStatus.Ok));
        }

        return items
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.Kind)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Task<string?> LoadSavedQueryTextAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _queries.LoadSavedQueryTextAsync(id, cancellationToken);
    }

    public Task<string?> LoadVisualizationTextAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _visualizations.LoadVisualizationQueryTextAsync(id, cancellationToken);
    }

    public Task DeleteAsync(
        LibraryItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        return DeleteAsync(item.Id, item.Kind, cancellationToken);
    }

    public Task DeleteAsync(
        string id,
        LibraryItemKind kind,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return kind switch
        {
            LibraryItemKind.SavedQuery => _queries.DeleteSavedQueryAsync(id, cancellationToken),
            LibraryItemKind.Visualization => _visualizations.DeleteVisualizationAsync(id, cancellationToken),
            LibraryItemKind.Dashboard => _dashboards.DeleteAsync(id, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported library item kind.")
        };
    }

    private static string ShortId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return "(none)";
        }

        return id.Length <= 8 ? id : id[..8];
    }
}

public sealed record LibraryItem(
    string Id,
    LibraryItemKind Kind,
    string Name,
    string? Description,
    string DependencyLabel,
    DateTime UpdatedAtUtc,
    LibraryItemStatus Status);

public enum LibraryItemKind
{
    SavedQuery,
    Visualization,
    Dashboard
}

public enum LibraryItemStatus
{
    Ok,
    MissingDependency
}
