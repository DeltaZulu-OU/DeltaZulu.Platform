
using DeltaZulu.Platform.Application.Hunting.Rendering.Tabular;
using DeltaZulu.Platform.Domain.Hunting.Rendering;

namespace DeltaZulu.Platform.Application.Hunting.Rendering.Services;
public interface IRenderResolver
{
    ResolvedRenderPlan Resolve(
        RenderDirective directive,
        IReadOnlyList<RenderColumn> columns);
}