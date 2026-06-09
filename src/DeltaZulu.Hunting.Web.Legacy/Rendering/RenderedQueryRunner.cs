namespace DeltaZulu.Hunting.Web.Rendering;

using DeltaZulu.Hunting.Render.Directives;
using DeltaZulu.Hunting.Render.Model;
using DeltaZulu.Hunting.Render.Services;

public sealed class RenderedQueryRunner : IRenderedQueryRunner
{
    private readonly IRenderChartBuilder _chartBuilder;
    private readonly IDataOnlyQueryService _queryService;
    private readonly IRenderDirectiveParser _renderDirectiveParser;

    public RenderedQueryRunner(
        IRenderDirectiveParser renderDirectiveParser,
        IDataOnlyQueryService queryService,
        IRenderChartBuilder chartBuilder)
    {
        _renderDirectiveParser = renderDirectiveParser ?? throw new ArgumentNullException(nameof(renderDirectiveParser));
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _chartBuilder = chartBuilder ?? throw new ArgumentNullException(nameof(chartBuilder));
    }

    public async Task<RenderedQueryResult> RunAsync(
        string queryText,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryText);

        var parsed = _renderDirectiveParser.Parse(queryText);
        return await RunCoreAsync(
            parsed.QueryTextWithoutRender,
            parsed.Directive,
            cancellationToken);
    }

    public async Task<RenderedQueryResult> RunAsync(
        string queryText,
        RenderDirective directive,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryText);
        ArgumentNullException.ThrowIfNull(directive);

        var parsed = _renderDirectiveParser.Parse(queryText);
        return await RunCoreAsync(
            parsed.QueryTextWithoutRender,
            directive,
            cancellationToken);
    }

    private async Task<RenderedQueryResult> RunCoreAsync(
        string dataQueryText,
        RenderDirective directive,
        CancellationToken cancellationToken)
    {
        var queryResult = await _queryService.ExecuteDataOnlyAsync(
            dataQueryText,
            cancellationToken);

        var chart = _chartBuilder.Build(
            new QueryResultRenderAdapter(queryResult),
            directive);

        return new RenderedQueryResult(
            queryResult,
            directive,
            chart);
    }
}
