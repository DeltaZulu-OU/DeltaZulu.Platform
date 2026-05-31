namespace Hunting.Core.Render;

public enum RenderKind
{
    Table,
    Card,
    Timechart,
    Linechart,
    Barchart,
    Columnchart,
    Piechart,
    Areachart,
    Scatterchart
}

public sealed record RenderSpec(RenderKind Kind, string? Title, string? XColumn, IReadOnlyList<string> YColumns, string? Series, string? Legend, bool IsStacked, bool IsFallback, string? FallbackReason);

public static class RenderSpecDefaults
{
    public static RenderSpec Table(string? reason = null) => new(RenderKind.Table, null, null, [], null, null, false, reason is not null, reason);
}