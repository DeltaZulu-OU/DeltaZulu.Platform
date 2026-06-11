namespace DeltaZulu.Platform.Application.Hunting.Render.Services;

using DeltaZulu.Platform.Application.Hunting.Render.Model;
using DeltaZulu.Platform.Application.Hunting.Render.Tabular;

public interface IRenderChartBuilder
{
    RenderChartModel Build(
        IRenderTabularResult result,
        RenderDirective directive);
}