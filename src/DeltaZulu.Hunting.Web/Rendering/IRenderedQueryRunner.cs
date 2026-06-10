namespace DeltaZulu.Hunting.Web.Rendering;

using DeltaZulu.Hunting.Render.Model;

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