
using DeltaZulu.Platform.Application.Analytics.Rendering.Tabular;
using DeltaZulu.Platform.Domain.Analytics.Rendering;

namespace DeltaZulu.Platform.Application.Analytics.Rendering.Services;
public interface IRenderResolver
{
    ResolvedRenderPlan Resolve(
        RenderDirective directive,
        IReadOnlyList<RenderColumn> columns);
}