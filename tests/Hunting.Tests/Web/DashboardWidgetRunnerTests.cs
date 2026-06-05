namespace Hunting.Tests.Web;

using Hunting.Core.Policy;
using Hunting.Data;
using Hunting.Render.Model;
using Hunting.Web.Dashboards;
using Hunting.Web.Dashboards.Runtime;
using Hunting.Web.Rendering;

[TestClass]
public sealed class DashboardWidgetRunnerTests
{
    [TestMethod]
    public async Task RunAsync_QueryWidget_SucceedsAndBuildsChartOptions()
    {
        var rendered = CreateRenderedResult(CreateSuccessfulQueryResult(), CreateRenderableChart());
        var fakeRunner = new FakeRenderedQueryRunner(rendered);
        var runner = CreateRunner(fakeRunner);
        var widget = CreateWidget();

        var result = await runner.RunAsync(widget, TestContext.CancellationToken);

        Assert.AreEqual(DashboardWidgetRunStatus.Succeeded, result.Status);
        Assert.AreEqual(widget.Id, result.WidgetId);
        Assert.AreSame(rendered.QueryResult, result.QueryResult);
        Assert.AreSame(rendered.Directive, result.RenderDirective);
        Assert.AreSame(rendered.Chart, result.Chart);
        Assert.IsNotNull(result.ChartOptions);
        Assert.IsTrue(result.HasRenderableChart);
        Assert.AreEqual(widget.QueryText, fakeRunner.LastQueryText);
        Assert.AreEqual(TestContext.CancellationToken, fakeRunner.LastCancellationToken);
    }

    [TestMethod]
    public async Task RunAsync_QueryWidgetWithTableFallback_SucceedsWithoutChartOptions()
    {
        var rendered = CreateRenderedResult(CreateSuccessfulQueryResult(), CreateTableFallbackChart(), RenderDirective.Table("No render directive."));
        var runner = CreateRunner(new FakeRenderedQueryRunner(rendered));

        var result = await runner.RunAsync(CreateWidget(), TestContext.CancellationToken);

        Assert.AreEqual(DashboardWidgetRunStatus.Succeeded, result.Status);
        Assert.IsNotNull(result.QueryResult);
        Assert.IsNotNull(result.Chart);
        Assert.IsNull(result.ChartOptions);
        Assert.IsFalse(result.HasRenderableChart);
    }

    [TestMethod]
    public async Task RunAsync_QueryFailure_ReturnsFailedStateAndDiagnostics()
    {
        var bag = new DiagnosticBag();
        bag.AddError(DiagnosticPhase.Translate, "Bad query.");
        var queryResult = QueryResult.FromDiagnostics(bag);
        var rendered = CreateRenderedResult(queryResult, CreateTableFallbackChart());
        var runner = CreateRunner(new FakeRenderedQueryRunner(rendered));

        var result = await runner.RunAsync(CreateWidget(), TestContext.CancellationToken);

        Assert.AreEqual(DashboardWidgetRunStatus.Failed, result.Status);
        Assert.AreSame(queryResult, result.QueryResult);
        Assert.HasCount(1, result.Diagnostics);
        Assert.AreEqual("Bad query.", result.Diagnostics[0].Message);
    }

    [TestMethod]
    public async Task RunAsync_NonQueryWidget_ReturnsFailedWithoutExecutingQuery()
    {
        var fakeRunner = new FakeRenderedQueryRunner(CreateRenderedResult(CreateSuccessfulQueryResult(), CreateRenderableChart()));
        var runner = CreateRunner(fakeRunner);
        var widget = CreateWidget() with
        {
            Kind = DashboardWidgetKind.Markdown,
            QueryText = string.Empty
        };

        var result = await runner.RunAsync(widget, TestContext.CancellationToken);

        Assert.AreEqual(DashboardWidgetRunStatus.Failed, result.Status);
        Assert.IsNull(fakeRunner.LastQueryText);
        Assert.HasCount(1, result.Diagnostics);
        Assert.Contains("not executable", result.Diagnostics[0].Message);
    }

    [TestMethod]
    public async Task RunAsync_InvalidQueryWidget_ReturnsFailedWithoutExecutingQuery()
    {
        var fakeRunner = new FakeRenderedQueryRunner(CreateRenderedResult(CreateSuccessfulQueryResult(), CreateRenderableChart()));
        var runner = CreateRunner(fakeRunner);
        var widget = CreateWidget() with { QueryText = string.Empty };

        var result = await runner.RunAsync(widget, TestContext.CancellationToken);

        Assert.AreEqual(DashboardWidgetRunStatus.Failed, result.Status);
        Assert.IsNull(fakeRunner.LastQueryText);
        Assert.IsNotEmpty(result.Diagnostics);
    }

    [TestMethod]
    public async Task RunAsync_RenderedRunnerThrows_ReturnsFailedState()
    {
        var fakeRunner = new FakeRenderedQueryRunner(new InvalidOperationException("Synthetic failure."));
        var runner = CreateRunner(fakeRunner);

        var result = await runner.RunAsync(CreateWidget(), TestContext.CancellationToken);

        Assert.AreEqual(DashboardWidgetRunStatus.Failed, result.Status);
        Assert.HasCount(1, result.Diagnostics);
        Assert.AreEqual("Widget execution failed.", result.Diagnostics[0].Message);
        Assert.AreEqual("Synthetic failure.", result.Diagnostics[0].DeveloperDetail);
    }

    [TestMethod]
    public async Task RunAsync_CancelledExecution_ReturnsCancelledState()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var fakeRunner = new FakeRenderedQueryRunner(new OperationCanceledException(cts.Token));
        var runner = CreateRunner(fakeRunner);

        var result = await runner.RunAsync(CreateWidget(), cts.Token);

        Assert.AreEqual(DashboardWidgetRunStatus.Cancelled, result.Status);
        Assert.HasCount(1, result.Diagnostics);
        Assert.AreEqual("Widget execution was cancelled.", result.Diagnostics[0].Message);
    }

    private static DashboardWidgetRunner CreateRunner(FakeRenderedQueryRunner renderedQueryRunner)
        => new(renderedQueryRunner, new EChartsRenderOptionsBuilder());

    private static DashboardWidgetDefinition CreateWidget()
        => new()
        {
            Id = "widget-1",
            Title = "Widget 1",
            QueryText = "ProcessEvent | summarize LaunchCount = count() by AccountName | render barchart xcolumn=AccountName ycolumns=LaunchCount"
        };

    private static RenderedQueryResult CreateRenderedResult(
        QueryResult queryResult,
        RenderChartModel chart,
        RenderDirective? directive = null)
        => new(
            queryResult,
            directive ?? new RenderDirective { Kind = chart.Kind },
            chart);

    private static QueryResult CreateSuccessfulQueryResult()
        => QueryResult.FromData(
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

    private static RenderChartModel CreateRenderableChart()
        => new(
            true,
            string.Empty,
            string.Empty,
            "AccountName",
            null,
            ["alice", "bob"],
            [new RenderSeries("LaunchCount", [3, 5])],
            0,
            5,
            null,
            false,
            RenderKind.Barchart);

    private static RenderChartModel CreateTableFallbackChart()
        => new(
            false,
            "Render fell back to table.",
            string.Empty,
            string.Empty,
            null,
            [],
            [],
            0,
            1,
            null,
            false,
            RenderKind.Table);

    private sealed class FakeRenderedQueryRunner : IRenderedQueryRunner
    {
        private readonly Exception? _exception;
        private readonly RenderedQueryResult? _result;

        public FakeRenderedQueryRunner(RenderedQueryResult result)
        {
            _result = result;
        }

        public FakeRenderedQueryRunner(Exception exception)
        {
            _exception = exception;
        }

        public string? LastQueryText { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }

        public Task<RenderedQueryResult> RunAsync(
            string queryText,
            CancellationToken cancellationToken = default)
        {
            LastQueryText = queryText;
            LastCancellationToken = cancellationToken;

            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(_result!);
        }
    }

    public TestContext TestContext { get; set; }
}
