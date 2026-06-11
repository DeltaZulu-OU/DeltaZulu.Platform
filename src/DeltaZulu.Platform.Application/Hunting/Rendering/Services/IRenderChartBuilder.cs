namespace DeltaZulu.Platform.Application.Hunting.Rendering.Services;

using DeltaZulu.Platform.Application.Hunting.Rendering.Tabular;
using DeltaZulu.Platform.Domain.Hunting.Rendering;

public interface IRenderChartBuilder
{
    RenderChartModel Build(
        IRenderTabularResult result,
        RenderDirective directive);
}