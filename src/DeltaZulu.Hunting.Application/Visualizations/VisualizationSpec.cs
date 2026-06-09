namespace Hunting.Application.Visualizations;

public sealed record VisualizationSpec
{
    public string? Title { get; init; }

    public string? XColumn { get; init; }

    public IReadOnlyList<string> YColumns { get; init; } = [];

    public string? SeriesColumn { get; init; }

    public string? Legend { get; init; }

    public bool IsStacked { get; init; }
}
