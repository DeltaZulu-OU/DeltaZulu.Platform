
using DeltaZulu.Platform.Data.DuckDb;
using DeltaZulu.Platform.Data.Analytics;
using DeltaZulu.Platform.Domain.Analytics.Policy;
using DeltaZulu.Platform.Domain.Analytics.Rendering;
using DeltaZulu.Platform.Domain.Analytics.SavedQueries;
using DeltaZulu.Platform.Domain.Analytics.Visualizations;
using DeltaZulu.Platform.Web.Analytics.Dashboards;
using DeltaZulu.Platform.Web.Analytics.Dashboards.Runtime;
using DeltaZulu.Platform.Web.Analytics.Rendering;
using Microsoft.Extensions.Logging;

namespace DeltaZulu.Platform.Tests.Analytics.Web;
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
        Assert.IsNull(fakeRunner.LastExplicitDirective);
        Assert.AreEqual(TestContext.CancellationToken, fakeRunner.LastCancellationToken);
    }

    [TestMethod]
    public async Task RunAsync_VisualizationWidget_LoadsSavedQueryAndAppliesVisualizationDirective()
    {
        var rendered = CreateRenderedResult(CreateSuccessfulQueryResult(), CreateRenderableChart());
        var fakeRunner = new FakeRenderedQueryRunner(rendered);
        var savedQueries = new FakeSavedQueryRepository();
        var visualizations = new FakeVisualizationRepository();
        var now = new DateTime(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

        savedQueries.Records["query-1"] = new SavedQueryRecord(
            "query-1",
            "Recent PowerShell",
            null,
            "ProcessEvent | summarize LaunchCount = count() by AccountName",
            now,
            now,
            null);

        visualizations.Records["visualization-1"] = new VisualizationRecord(
            "visualization-1",
            "query-1",
            "Launches by account",
            null,
            "barchart",
            """
            {
              "title": "Launches by account",
              "xColumn": "AccountName",
              "yColumns": [ "LaunchCount" ],
              "legend": "right"
            }
            """,
            now,
            now);

        var runner = CreateRunner(fakeRunner, savedQueries, visualizations);
        var widget = CreateWidget() with
        {
            QueryText = string.Empty,
            VisualizationId = "visualization-1"
        };

        var result = await runner.RunAsync(widget, TestContext.CancellationToken);

        Assert.AreEqual(DashboardWidgetRunStatus.Succeeded, result.Status);
        Assert.AreEqual("ProcessEvent | summarize LaunchCount = count() by AccountName", fakeRunner.LastQueryText);
        Assert.IsNotNull(fakeRunner.LastExplicitDirective);
        Assert.AreEqual(RenderKind.Barchart, fakeRunner.LastExplicitDirective.Kind);
        Assert.AreEqual("Launches by account", fakeRunner.LastExplicitDirective.Title);
        Assert.AreEqual("AccountName", fakeRunner.LastExplicitDirective.Binding.XColumn);
        CollectionAssert.AreEqual(
            new[] { "LaunchCount" },
            fakeRunner.LastExplicitDirective.Binding.YColumns.ToArray());
        Assert.AreEqual("right", fakeRunner.LastExplicitDirective.Legend);
    }

    [TestMethod]
    public async Task RunAsync_QueryWidget_WritesExecutionDetailsToDebugLog()
    {
        var logger = new RecordingLogger<DashboardWidgetRunner>();
        var runner = CreateRunner(
            new FakeRenderedQueryRunner(CreateRenderedResult(CreateSuccessfulQueryResult(), CreateRenderableChart())),
            logger: logger);

        var result = await runner.RunAsync(CreateWidget(), TestContext.CancellationToken);

        Assert.AreEqual(DashboardWidgetRunStatus.Succeeded, result.Status);
        Assert.Contains(
            entry =>
                entry.Level == LogLevel.Debug
                    && entry.Message.Contains("widget-1", StringComparison.OrdinalIgnoreCase)
                    && entry.Message.Contains("Widget 1", StringComparison.OrdinalIgnoreCase)
                    && entry.Message.Contains("query text", StringComparison.OrdinalIgnoreCase)
                    && entry.Message.Contains("Succeeded", StringComparison.OrdinalIgnoreCase)
                    && entry.Message.Contains("XColumn=auto", StringComparison.OrdinalIgnoreCase)
                    && entry.Message.Contains("SeriesCount=1", StringComparison.OrdinalIgnoreCase), logger.Entries,
            "Expected execution metadata to be available through Debug logging instead of chart chrome.");
    }

    [TestMethod]
    public async Task RunAsync_InvalidQueryWidget_WritesFailureDetailsToDebugLog()
    {
        var logger = new RecordingLogger<DashboardWidgetRunner>();
        var fakeRunner = new FakeRenderedQueryRunner(CreateRenderedResult(CreateSuccessfulQueryResult(), CreateRenderableChart()));
        var runner = CreateRunner(fakeRunner, logger: logger);
        var widget = CreateWidget() with { QueryText = string.Empty };

        var result = await runner.RunAsync(widget, TestContext.CancellationToken);

        Assert.AreEqual(DashboardWidgetRunStatus.Failed, result.Status);
        Assert.Contains(
            entry =>
                entry.Level == LogLevel.Debug
                    && entry.Message.Contains("widget-1", StringComparison.OrdinalIgnoreCase)
                    && entry.Message.Contains("Failed", StringComparison.OrdinalIgnoreCase), logger.Entries,
            "Expected failed widget execution metadata to be available through Debug logging.");
    }

    [TestMethod]
    public async Task RunAsync_VisualizationWidgetWithMissingVisualization_ReturnsFailedState()
    {
        var fakeRunner = new FakeRenderedQueryRunner(CreateRenderedResult(CreateSuccessfulQueryResult(), CreateRenderableChart()));
        var runner = CreateRunner(fakeRunner);
        var widget = CreateWidget() with
        {
            QueryText = string.Empty,
            VisualizationId = "missing-visualization"
        };

        var result = await runner.RunAsync(widget, TestContext.CancellationToken);

        Assert.AreEqual(DashboardWidgetRunStatus.Failed, result.Status);
        Assert.HasCount(1, result.Diagnostics);
        Assert.Contains("was not found", result.Diagnostics[0].Message);
        Assert.IsNull(fakeRunner.LastQueryText);
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

    private static DashboardWidgetRunner CreateRunner(
        FakeRenderedQueryRunner renderedQueryRunner,
        FakeSavedQueryRepository? savedQueries = null,
        FakeVisualizationRepository? visualizations = null,
        RecordingLogger<DashboardWidgetRunner>? logger = null)
        => new(
            renderedQueryRunner,
            new EChartsRenderOptionsBuilder(),
            savedQueries ?? new FakeSavedQueryRepository(),
            visualizations ?? new FakeVisualizationRepository(),
            logger ?? new RecordingLogger<DashboardWidgetRunner>());

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
        public RenderDirective? LastExplicitDirective { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }

        public Task<RenderedQueryResult> RunAsync(
            string queryText,
            CancellationToken cancellationToken = default)
        {
            LastQueryText = queryText;
            LastExplicitDirective = null;
            LastCancellationToken = cancellationToken;

            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(_result!);
        }

        public Task<RenderedQueryResult> RunAsync(
            string queryText,
            RenderDirective directive,
            CancellationToken cancellationToken = default)
        {
            LastQueryText = queryText;
            LastExplicitDirective = directive;
            LastCancellationToken = cancellationToken;

            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(_result!);
        }
    }

    private sealed class FakeSavedQueryRepository : ISavedQueryRepository
    {
        public Dictionary<string, SavedQueryRecord> Records { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            Records.Remove(id);
            return Task.CompletedTask;
        }

        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<SavedQueryRecord?> GetAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(Records.TryGetValue(id, out var record) ? record : null);

        public Task<IReadOnlyList<SavedQueryRecord>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SavedQueryRecord>>(Records.Values.ToArray());

        public Task MarkRunAsync(string id, DateTime runAt, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveAsync(SavedQueryRecord query, CancellationToken cancellationToken = default)
        {
            Records[query.Id] = query;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeVisualizationRepository : IVisualizationRepository
    {
        public Dictionary<string, VisualizationRecord> Records { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            Records.Remove(id);
            return Task.CompletedTask;
        }

        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<VisualizationRecord?> GetAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(Records.TryGetValue(id, out var record) ? record : null);

        public Task<IReadOnlyList<VisualizationRecord>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VisualizationRecord>>(Records.Values.ToArray());

        public Task<IReadOnlyList<VisualizationRecord>> ListByQueryAsync(string queryId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VisualizationRecord>>(
                Records.Values
                    .Where(record => string.Equals(record.QueryId, queryId, StringComparison.OrdinalIgnoreCase))
                    .ToArray());

        public Task SaveAsync(VisualizationRecord visualization, CancellationToken cancellationToken = default)
        {
            Records[visualization.Id] = visualization;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    public TestContext TestContext { get; set; }
}