namespace DeltaZulu.Platform.Web.Hunting.Services;

using System.Text.Json;
using DeltaZulu.Platform.Application.Hunting.Rendering.Directives;
using DeltaZulu.Platform.Domain.Hunting.Rendering;
using DeltaZulu.Platform.Domain.Hunting.SavedQueries;
using DeltaZulu.Platform.Domain.Hunting.Visualizations;
using DeltaZulu.Platform.Web.Hunting.Dashboards.Persistence;

/// <summary>
/// Application-facing service for saved visualization definitions.
/// This keeps UI components from depending directly on visualization persistence rows
/// and centralizes validation/serialization for dashboard-ready visualizations.
/// </summary>
public sealed class VisualizationLibraryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDashboardRepository _dashboards;
    private readonly IRenderDirectiveParser _renderDirectiveParser;
    private readonly ISavedQueryRepository _savedQueries;
    private readonly IVisualizationRepository _visualizations;

    public VisualizationLibraryService(
        ISavedQueryRepository savedQueries,
        IVisualizationRepository visualizations,
        IRenderDirectiveParser renderDirectiveParser,
        IDashboardRepository dashboards)
    {
        _savedQueries = savedQueries ?? throw new ArgumentNullException(nameof(savedQueries));
        _visualizations = visualizations ?? throw new ArgumentNullException(nameof(visualizations));
        _renderDirectiveParser = renderDirectiveParser ?? throw new ArgumentNullException(nameof(renderDirectiveParser));
        _dashboards = dashboards ?? throw new ArgumentNullException(nameof(dashboards));
    }

    public Task<IReadOnlyList<VisualizationRecord>> ListVisualizationsAsync(
        CancellationToken cancellationToken = default) => _visualizations.ListAsync(cancellationToken);

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

    public async Task<string?> LoadVisualizationQueryTextAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var visualization = await _visualizations.GetAsync(id, cancellationToken);
        if (visualization is null)
        {
            return null;
        }

        var query = await _savedQueries.GetAsync(visualization.QueryId, cancellationToken);
        if (query is null)
        {
            return null;
        }

        if (!Enum.TryParse<RenderKind>(visualization.Kind, ignoreCase: true, out var kind))
        {
            throw new InvalidOperationException(
                $"Visualization '{visualization.Id}' has unsupported kind '{visualization.Kind}'.");
        }

        var spec = DeserializeVisualizationSpec(visualization);
        var renderClause = BuildRenderClause(kind, spec);
        return $"{query.QueryText.TrimEnd()}{Environment.NewLine}{renderClause}";
    }

    public async Task<SavedVisualizationResult> SaveVisualizationFromRenderedQueryAsync(
        string? queryId,
        string? visualizationId,
        string name,
        string? description,
        string queryText,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryText);

        var parsed = _renderDirectiveParser.Parse(queryText);
        if (string.Equals(parsed.QueryTextWithoutRender, queryText, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The current query must include a terminal render clause before it can be saved as a visualization.");
        }

        if (parsed.Directive.IsFallback)
        {
            throw new InvalidOperationException(
                parsed.Directive.FallbackReason ?? "The render directive could not be parsed.");
        }

        var now = DateTime.UtcNow;
        var normalizedQueryId = string.IsNullOrWhiteSpace(queryId)
            ? Guid.NewGuid().ToString("N")
            : queryId.Trim();

        var existingQuery = await _savedQueries.GetAsync(normalizedQueryId, cancellationToken);
        var savedQuery = new SavedQueryRecord(
            normalizedQueryId,
            name.Trim(),
            NormalizeOptionalText(description),
            parsed.QueryTextWithoutRender.Trim(),
            existingQuery?.CreatedAt ?? now,
            now,
            existingQuery?.LastRunAt);

        await _savedQueries.SaveAsync(savedQuery, cancellationToken);

        var visualization = await SaveVisualizationAsync(
            visualizationId,
            savedQuery.Id,
            name,
            description,
            parsed.Directive.Kind,
            ToVisualizationSpec(parsed.Directive),
            cancellationToken);

        return new SavedVisualizationResult(savedQuery, visualization);
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
        var query = await _savedQueries.GetAsync(normalizedQueryId, cancellationToken) ?? throw new InvalidOperationException(
                $"Saved query '{normalizedQueryId}' was not found. A visualization must reference an existing saved query.");
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

    public async Task<IReadOnlyList<VisualizationDashboardUsage>> ListDashboardUsagesAsync(
        string visualizationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(visualizationId);

        var usages = new List<VisualizationDashboardUsage>();
        var summaries = await _dashboards.ListAsync(cancellationToken);
        foreach (var summary in summaries)
        {
            var dashboard = await _dashboards.GetAsync(summary.Id, cancellationToken);
            if (dashboard is null)
            {
                continue;
            }

            foreach (var widget in dashboard.Widgets)
            {
                if (!string.Equals(widget.VisualizationId, visualizationId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                usages.Add(new VisualizationDashboardUsage(
                    dashboard.Id,
                    dashboard.Name,
                    widget.Id,
                    widget.Title));
            }
        }

        return usages;
    }

    public async Task DeleteVisualizationAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var usages = await ListDashboardUsagesAsync(id, cancellationToken);
        if (usages.Count > 0)
        {
            throw new InvalidOperationException(CreateInUseDeleteMessage(id, usages));
        }

        await _visualizations.DeleteAsync(id, cancellationToken);
    }

    private static string CreateInUseDeleteMessage(
        string visualizationId,
        IReadOnlyList<VisualizationDashboardUsage> usages)
    {
        var dashboardNames = usages
            .Select(usage => string.IsNullOrWhiteSpace(usage.DashboardName) ? usage.DashboardId : usage.DashboardName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();

        var suffix = usages.Count > dashboardNames.Length ? "…" : string.Empty;
        return $"Visualization '{visualizationId}' is used by {usages.Count} dashboard widget(s): {string.Join(", ", dashboardNames)}{suffix}. Remove it from dashboards before deleting it.";
    }

    private static VisualizationSpec ToVisualizationSpec(RenderDirective directive)
        => new()
        {
            Title = NormalizeOptionalText(directive.Title),
            XColumn = NormalizeOptionalText(directive.Binding.XColumn),
            YColumns = NormalizeColumnList(directive.Binding.YColumns),
            SeriesColumn = NormalizeOptionalText(directive.Binding.SeriesColumn),
            Legend = NormalizeOptionalText(directive.Legend),
            IsStacked = directive.IsStacked
        };

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

    private static VisualizationSpec DeserializeVisualizationSpec(VisualizationRecord visualization)
    {
        try
        {
            return JsonSerializer.Deserialize<VisualizationSpec>(
                    visualization.SpecJson,
                    JsonOptions)
                ?? new VisualizationSpec();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Visualization '{visualization.Id}' contains malformed visualization spec JSON.",
                ex);
        }
    }

    private static string BuildRenderClause(RenderKind kind, VisualizationSpec spec)
    {
        var properties = new List<string>();

        AddRenderProperty(properties, "title", spec.Title);
        AddRenderProperty(properties, "xcolumn", spec.XColumn);
        AddRenderProperty(properties, "ycolumns", spec.YColumns.Count == 0 ? null : string.Join(",", spec.YColumns));
        AddRenderProperty(properties, "series", spec.SeriesColumn);
        AddRenderProperty(properties, "legend", spec.Legend);

        if (spec.IsStacked)
        {
            AddRenderProperty(properties, "kind", "stacked");
        }

        var kindText = kind.ToString().ToLowerInvariant();
        return properties.Count == 0
            ? $"| render {kindText}"
            : $"| render {kindText} with ({string.Join(", ", properties)})";
    }

    private static void AddRenderProperty(List<string> properties, string name, string? value)
    {
        var normalized = NormalizeOptionalText(value);
        if (normalized is null)
        {
            return;
        }

        properties.Add($"{name}={QuoteRenderValue(normalized)}");
    }

    private static string QuoteRenderValue(string value)
        => $"'{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal)}'";

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

public sealed record SavedVisualizationResult(
    SavedQueryRecord Query,
    VisualizationRecord Visualization);

public sealed record VisualizationDashboardUsage(
    string DashboardId,
    string DashboardName,
    string WidgetId,
    string WidgetTitle);