namespace DeltaZulu.Platform.Application.Hunting.Render.Model;

public sealed record RenderSeries(
    string Name,
    IReadOnlyList<double> Values);