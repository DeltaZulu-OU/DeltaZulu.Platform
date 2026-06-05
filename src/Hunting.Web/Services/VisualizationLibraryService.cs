namespace Hunting.Web.Services;

using System.Text.Json;
using Hunting.Application.SavedQueries;
using Hunting.Application.Visualizations;
using Hunting.Render.Model;

/// <summary>
/// Application-facing service for saved visualization definitions.
/// This keeps UI components from depending directly on visualization persistence rows
/// and centralizes validation/serialization for dashboard-ready visualizations.
/// </summary>
public sealed class VisualizationLibraryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ISavedQueryRepository _savedQueries;
    private readonly IVisualizationRepository _visualizations;

    public VisualizationLibraryService(
        ISavedQueryRepository savedQueries,
        IVisualizationRepository visualizations)
    {
        _savedQueries = savedQueries ?? throw new ArgumentNullException(nameof(savedQueries));
        _visualizations = visualizations ?? throw new ArgumentNullException(nameof(visualizations));
    }

    public Task<IReadOnlyList<VisualizationRecord>> ListVisualizationsAsync(
        CancellationToken cancellationToken = default)
    {
        return _visualizations.ListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<VisualizationRecord>> ListVisualizationsByQueryAsync(
        string queryId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryId);
        return _visualizations.ListByQueryAsync(queryId, cancellationToken);
    }

    public Task<VisualizationRecord?> GetVisualizationAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _visualizations.GetAsync(id, cancellationToken);
    }

    public async Task<VisualizationRecord> SaveVisualizationAsync(
        string? id,
        string queryId,
        string name,
        string? description,
        RenderKind kind,
        VisualizationSpec spec,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(spec);

        var normalizedQueryId = queryId.Trim();
        var query = await _savedQueries.GetAsync(normalizedQueryId, cancellationToken);
        if (query is null)
        {
            throw new InvalidOperationException(
                $"Saved query '{normalizedQueryId}' was not found. A visualization must reference an existing saved query.");
        }

        var normalizedId = string.IsNullOrWhiteSpace(id)
            ? Guid.NewGuid().ToString("N")
            : id.Trim();

        var now = DateTime.UtcNow;
        var existing = await _visualizations.GetAsync(normalizedId, cancellationToken);

        var record = new VisualizationRecord(
            normalizedId,
            normalizedQueryId,
            name.Trim(),
            NormalizeOptionalText(description),
            kind.ToString(),
            SerializeSpec(NormalizeSpec(spec)),
            existing?.CreatedAt ?? now,
            now);

        await _visualizations.SaveAsync(record, cancellationToken);
        return record;
    }

    public Task DeleteVisualizationAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _visualizations.DeleteAsync(id, cancellationToken);
    }

    private static VisualizationSpec NormalizeSpec(VisualizationSpec spec)
        => new()
        {
            Title = NormalizeOptionalText(spec.Title),
            XColumn = NormalizeOptionalText(spec.XColumn),
            YColumns = NormalizeColumnList(spec.YColumns),
            SeriesColumn = NormalizeOptionalText(spec.SeriesColumn),
            Legend = NormalizeOptionalText(spec.Legend),
            IsStacked = spec.IsStacked
        };

    private static string SerializeSpec(VisualizationSpec spec)
        => JsonSerializer.Serialize(spec, JsonOptions);

    private static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<string> NormalizeColumnList(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return [];
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
