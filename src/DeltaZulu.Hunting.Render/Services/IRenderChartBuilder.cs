namespace DeltaZulu.Hunting.Render.Services;

using DeltaZulu.Hunting.Render.Model;
using DeltaZulu.Hunting.Render.Tabular;

public interface IRenderChartBuilder
{
    RenderChartModel Build(
        IRenderTabularResult result,
        RenderDirective directive);
}