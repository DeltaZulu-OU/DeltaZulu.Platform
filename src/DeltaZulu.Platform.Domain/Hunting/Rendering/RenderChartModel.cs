namespace DeltaZulu.Platform.Application.Hunting.Render.Model;

public sealed record RenderChartModel(
    bool CanRender,
    string Message,
    string Warning,
    string XColumn,
    string? SeriesColumn,
    IReadOnlyList<string> XLabels,
    IReadOnlyList<RenderSeries> Series,
    double YMin,
    double YMax,
    string? Legend,
    bool IsStacked,
    RenderKind Kind);