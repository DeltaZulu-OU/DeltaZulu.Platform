namespace Hunting.Web.Rendering;

public interface IRenderedQueryRunner
{
    Task<RenderedQueryResult> RunAsync(
        string queryText,
        CancellationToken cancellationToken = default);
}
