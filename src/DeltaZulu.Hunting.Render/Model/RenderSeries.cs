namespace DeltaZulu.Hunting.Render.Model;

public sealed record RenderSeries(
    string Name,
    IReadOnlyList<double> Values);
