namespace Hunting.Web.Rendering;

using Hunting.Render.Directives;
using Hunting.Render.Services;

public sealed class RenderedQueryRunner
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
        var queryResult = await _queryService.ExecuteDataOnlyAsync(
            parsed.QueryTextWithoutRender,
            cancellationToken);

        var chart = _chartBuilder.Build(
            new QueryResultRenderAdapter(queryResult),
            parsed.Directive);

        return new RenderedQueryResult(
            queryResult,
            parsed.Directive,
            chart);
    }
}
