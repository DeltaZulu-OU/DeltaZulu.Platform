namespace DeltaZulu.Platform.Tests.Hunting.Web;

using DeltaZulu.Platform.Application.Hunting.Render.Directives;using DeltaZulu.Platform.Data.DuckDb;
using DeltaZulu.Platform.Data.Hunting;using DeltaZulu.Platform.Domain.Hunting.Policy;using DeltaZulu.Platform.Domain.Hunting.Rendering;
using DeltaZulu.Platform.Web.Hunting.Rendering;
[TestClass]public sealed class RenderedQueryRunnerTests
{
    [TestMethod]
    public async Task RunAsync_RenderDirective_ExecutesStrippedDataQueryAndBuildsChart()
    {
        var queryService = new CapturingDataOnlyQueryService(CreateResult());
        var runner = new RenderedQueryRunner(
            new RenderDirectiveParser(),
            queryService,
            new RenderChartBuilder());

        var rendered = await runner.RunAsync(
            "ProcessEvent | summarize LaunchCount = count() by AccountName | render barchart xcolumn=AccountName ycolumns=LaunchCount", TestContext.CancellationToken);

        Assert.AreEqual("ProcessEvent | summarize LaunchCount = count() by AccountName", queryService.LastQuery);
        Assert.AreEqual(RenderKind.Barchart, rendered.Directive.Kind);
        Assert.IsTrue(rendered.Chart.CanRender);
        Assert.AreEqual("AccountName", rendered.Chart.XColumn);
        Assert.HasCount(1, rendered.Chart.Series);
        Assert.AreEqual("LaunchCount", rendered.Chart.Series[0].Name);
    }

    [TestMethod]
    public async Task RunAsync_NoRenderDirective_ExecutesOriginalQueryAndReturnsTableFallbackChart()
    {
        var queryService = new CapturingDataOnlyQueryService(CreateResult());
        var runner = new RenderedQueryRunner(
            new RenderDirectiveParser(),
            queryService,
            new RenderChartBuilder());

        var rendered = await runner.RunAsync("ProcessEvent | take 10", TestContext.CancellationToken);

        Assert.AreEqual("ProcessEvent | take 10", queryService.LastQuery);
        Assert.AreEqual(RenderKind.Table, rendered.Directive.Kind);
        Assert.IsFalse(rendered.Chart.CanRender);
        Assert.AreEqual("Render fell back to table.", rendered.Chart.Message);
    }

    [TestMethod]
    public async Task RunAsync_QueryFailure_ReturnsFailedResultAndFallbackChart()
    {
        var bag = new DiagnosticBag();
        bag.AddError(DiagnosticPhase.Translate, "Bad query.");
        var failedResult = QueryResult.FromDiagnostics(bag);

        var queryService = new CapturingDataOnlyQueryService(failedResult);
        var runner = new RenderedQueryRunner(
            new RenderDirectiveParser(),
            queryService,
            new RenderChartBuilder());

        var rendered = await runner.RunAsync("BadQuery | render linechart", TestContext.CancellationToken);

        Assert.AreSame(failedResult, rendered.QueryResult);
        Assert.AreEqual("BadQuery", queryService.LastQuery);
        Assert.IsFalse(rendered.Chart.CanRender);
        Assert.AreEqual("No render data.", rendered.Chart.Message);
    }

    [TestMethod]
    public async Task RunAsync_PassesCancellationTokenToDataOnlyQueryService()
    {
        using var cts = new CancellationTokenSource();
        var queryService = new CapturingDataOnlyQueryService(CreateResult());
        var runner = new RenderedQueryRunner(
            new RenderDirectiveParser(),
            queryService,
            new RenderChartBuilder());

        await runner.RunAsync("ProcessEvent | take 1", cts.Token);

        Assert.AreEqual(cts.Token, queryService.LastCancellationToken);
    }

    [TestMethod]
    public async Task RunAsync_BlankQuery_Throws()
    {
        var runner = new RenderedQueryRunner(
            new RenderDirectiveParser(),
            new CapturingDataOnlyQueryService(CreateResult()),
            new RenderChartBuilder());

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => runner.RunAsync("", TestContext.CancellationToken));
    }

    private static QueryResult CreateResult() => QueryResult.FromData(
            [
                new ResultColumn("AccountName", "VARCHAR"),
                new ResultColumn("LaunchCount", "BIGINT")
            ],
            [
                ["alice", "bob"],
                [3L, 5L]
            ],
            null,
            null,
            null,
            null,
            new DiagnosticBag());

    private sealed class CapturingDataOnlyQueryService : IDataOnlyQueryService
    {
        private readonly QueryResult _result;

        public CapturingDataOnlyQueryService(QueryResult result)
        {
            _result = result;
        }

        public string? LastQuery { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public Task<QueryResult> ExecuteDataOnlyAsync(
            string kql,
            CancellationToken ct = default)
        {
            LastQuery = kql;
            LastCancellationToken = ct;
            return Task.FromResult(_result);
        }
    }

    public TestContext TestContext { get; set; }
}