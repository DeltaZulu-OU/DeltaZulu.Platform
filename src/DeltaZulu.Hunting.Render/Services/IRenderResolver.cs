namespace DeltaZulu.Hunting.Render.Services;

using DeltaZulu.Hunting.Render.Model;
using DeltaZulu.Hunting.Render.Tabular;

public interface IRenderResolver
{
    ResolvedRenderPlan Resolve(
        RenderDirective directive,
        IReadOnlyList<RenderColumn> columns);
}