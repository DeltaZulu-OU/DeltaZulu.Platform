namespace DeltaZulu.Platform.Application.Hunting.Rendering.Services;

using DeltaZulu.Platform.Application.Hunting.Rendering.Tabular;
using DeltaZulu.Platform.Domain.Hunting.Rendering;

public interface IRenderResolver
{
    ResolvedRenderPlan Resolve(
        RenderDirective directive,
        IReadOnlyList<RenderColumn> columns);
}