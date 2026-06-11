namespace DeltaZulu.Platform.Domain.Analytics.Rendering;

public sealed record RenderSeries(
    string Name,
    IReadOnlyList<double> Values);