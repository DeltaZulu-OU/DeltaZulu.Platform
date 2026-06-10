namespace DeltaZulu.Hunting.Render.Model;

public sealed record RenderBinding
{
    public string? XColumn { get; init; }

    public IReadOnlyList<string> YColumns { get; init; } = [];

    public string? SeriesColumn { get; init; }
}