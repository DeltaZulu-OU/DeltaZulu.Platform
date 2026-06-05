namespace Hunting.Web.Dashboards;

public sealed record DashboardLayout
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; } = 4;
    public int Height { get; init; } = 3;
    public int MinimumWidth { get; init; } = 1;
    public int MinimumHeight { get; init; } = 1;
}
