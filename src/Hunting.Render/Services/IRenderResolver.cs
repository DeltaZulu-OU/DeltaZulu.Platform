namespace Hunting.Render.Services;

using Hunting.Render.Model;
using Hunting.Render.Tabular;

public interface IRenderResolver
{
    ResolvedRenderPlan Resolve(
        RenderDirective directive,
        IReadOnlyList<RenderColumn> columns);
}
