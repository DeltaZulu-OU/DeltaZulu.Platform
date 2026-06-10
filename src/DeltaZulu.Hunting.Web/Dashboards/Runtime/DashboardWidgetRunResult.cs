namespace DeltaZulu.Hunting.Web.Dashboards.Runtime;

using DeltaZulu.Hunting.Core.Policy;
using DeltaZulu.Hunting.Data;
using DeltaZulu.Hunting.Render.Model;
using Vizor.ECharts;

public sealed record DashboardWidgetRunResult
{
    public string WidgetId { get; init; } = string.Empty;
    public DashboardWidgetRunStatus Status { get; init; }
    public QueryResult? QueryResult { get; init; }
    public RenderDirective? RenderDirective { get; init; }
    public RenderChartModel? Chart { get; init; }
    public ChartOptions? ChartOptions { get; init; }
    public IReadOnlyList<QueryDiagnostic> Diagnostics { get; init; } = [];
    public DateTime StartedAtUtc { get; init; }
    public TimeSpan Duration { get; init; }
    public string? VisualizationId { get; init; }
    public string? VisualizationName { get; init; }
    public string? SavedQueryId { get; init; }
    public string? SavedQueryName { get; init; }

    public bool HasRenderableChart => Chart?.CanRender == true && ChartOptions is not null;
}
