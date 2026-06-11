
using DeltaZulu.Platform.Application.Hunting.Rendering.Tabular;
using DeltaZulu.Platform.Domain.Hunting.Rendering;

namespace DeltaZulu.Platform.Application.Hunting.Rendering.Services;
public interface IRenderChartBuilder
{
    public RenderChartModel Build(
        IRenderTabularResult result,
        RenderDirective directive);
}