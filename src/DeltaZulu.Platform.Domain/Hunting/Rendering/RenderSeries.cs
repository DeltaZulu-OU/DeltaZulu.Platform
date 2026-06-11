namespace DeltaZulu.Platform.Domain.Hunting.Rendering;

public sealed record RenderSeries(
    string Name,
    IReadOnlyList<double> Values);