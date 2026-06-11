namespace DeltaZulu.Platform.Web.Hunting.Rendering;

using DeltaZulu.Platform.Application.Hunting.Render.Model;

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