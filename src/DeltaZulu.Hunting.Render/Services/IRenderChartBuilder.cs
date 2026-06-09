namespace Hunting.Render.Services;

using Hunting.Render.Model;
using Hunting.Render.Tabular;

public interface IRenderChartBuilder
{
    RenderChartModel Build(
        IRenderTabularResult result,
        RenderDirective directive);
}
