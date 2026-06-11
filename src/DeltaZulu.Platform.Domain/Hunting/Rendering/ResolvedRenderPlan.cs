namespace DeltaZulu.Platform.Application.Hunting.Render.Model;

public sealed record ResolvedRenderPlan(
    RenderKind Kind,
    string? Title,
    string? XColumn,
    IReadOnlyList<string> YColumns,
    string? SeriesColumn,
    string? Legend,
    bool IsStacked,
    bool IsFallback,
    string? FallbackReason);