namespace DeltaZulu.Platform.Domain.Hunting.Rendering;

public sealed record RenderBinding
{
    public string? XColumn { get; init; }

    public IReadOnlyList<string> YColumns { get; init; } = [];

    public string? SeriesColumn { get; init; }
}