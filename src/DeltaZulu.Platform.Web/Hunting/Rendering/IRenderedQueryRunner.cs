
using DeltaZulu.Platform.Domain.Hunting.Rendering;

namespace DeltaZulu.Platform.Web.Hunting.Rendering;
public interface IRenderedQueryRunner
{
    Task<RenderedQueryResult> RunAsync(
        string queryText,
        CancellationToken cancellationToken = default);

    Task<RenderedQueryResult> RunAsync(
        string queryText,
        RenderDirective directive,
        CancellationToken cancellationToken = default);
}