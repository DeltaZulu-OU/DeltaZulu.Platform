namespace DeltaZulu.Hunting.Web.Dashboards;

public sealed record DashboardWidgetDefinition
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; init; } = string.Empty;
    public DashboardWidgetKind Kind { get; init; } = DashboardWidgetKind.Query;
    public string? VisualizationId { get; init; }
    public string QueryText { get; init; } = string.Empty;
    public DashboardLayout Layout { get; init; } = new();
    public DashboardRefreshPolicy Refresh { get; init; } = DashboardRefreshPolicy.Manual();
}