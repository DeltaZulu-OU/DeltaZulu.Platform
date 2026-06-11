namespace DeltaZulu.Platform.Web.Hunting.Dashboards;

public sealed record DashboardDefinition
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DashboardRefreshPolicy Refresh { get; init; } = DashboardRefreshPolicy.Manual();
    public IReadOnlyList<DashboardWidgetDefinition> Widgets { get; init; } = [];
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;

    public bool IsValid() => !string.IsNullOrWhiteSpace(Name) && Widgets.Count != 0;
}