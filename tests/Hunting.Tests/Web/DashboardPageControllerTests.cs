namespace Hunting.Tests.Web;

using System.Diagnostics;
using Hunting.Application.SavedQueries;
using Hunting.Application.Visualizations;
using Hunting.Core.Policy;
using Hunting.Data;
using Hunting.Render.Model;
using Hunting.Web.Dashboards;
using Hunting.Web.Dashboards.PageState;
using Hunting.Web.Dashboards.Persistence;
using Hunting.Web.Dashboards.Runtime;
using Hunting.Web.Rendering;
using Microsoft.Extensions.Logging;

[TestClass]
public sealed class DashboardPageControllerTests
{
    [TestMethod]
    public async Task LoadAsync_ExistingDashboard_LoadsDashboardAndClearsLoading()
    {
        var dashboard = CreateDashboard(widgets: []);
        var repository = new FakeDashboardRepository(dashboard);
        var controller = CreateController(repository);

        await controller.LoadAsync(dashboard.Id);

        Assert.IsFalse(controller.State.Loading);
        Assert.IsNull(controller.State.Error);
        Assert.AreSame(dashboard, controller.State.Dashboard);
    }

    [TestMethod]
    public async Task LoadAsync_MissingDashboard_SetsError()
    {
        var controller = CreateController(new FakeDashboardRepository());

        await controller.LoadAsync("missing-dashboard");

        Assert.IsFalse(controller.State.Loading);
        Assert.IsNull(controller.State.Dashboard);
        Assert.AreEqual("The requested dashboard was not found.", controller.State.Error);
    }

    [TestMethod]
    public async Task SaveWidgetAsync_NewQueryWidget_SavesAndRunsWidget()
    {
        var dashboard = CreateDashboard(widgets: []);
        var repository = new FakeDashboardRepository(dashboard);
        var renderedRunner = new FakeRenderedQueryRunner(CreateRenderedResult(CreateSuccessfulQueryResult(), CreateTableFallbackChart()));
        var controller = CreateController(repository, renderedRunner);

        await controller.LoadAsync(dashboard.Id);
        var widget = CreateWidget("widget-1");

        await controller.SaveWidgetAsync(widget);

        Assert.IsNull(controller.State.SaveError);
        Assert.IsFalse(controller.State.EditorOpen);
        Assert.AreEqual(1, repository.SaveCount);
        Assert.HasCount(1, controller.State.Dashboard!.Widgets);
        Assert.AreEqual(widget.Id, controller.State.Dashboard.Widgets[0].Id);
        Assert.AreEqual(1, renderedRunner.RunCount);
        Assert.AreEqual(widget.QueryText, renderedRunner.LastQueryText);
        Assert.IsNull(renderedRunner.LastDirective);
        Assert.IsTrue(controller.State.WidgetResults.ContainsKey(widget.Id));
        Assert.AreEqual(DashboardWidgetRunStatus.Succeeded, controller.State.WidgetResults[widget.Id].Status);
    }

    [TestMethod]
    public async Task DeleteWidgetAsync_RemovesWidgetAndResult()
    {
        var widget = CreateWidget("widget-1");
        var dashboard = CreateDashboard(widgets: [widget]);
        var repository = new FakeDashboardRepository(dashboard);
        var controller = CreateController(repository);

        await controller.LoadAsync(dashboard.Id);
        controller.State.WidgetResults[widget.Id] = new DashboardWidgetRunResult
        {
            WidgetId = widget.Id,
            Status = DashboardWidgetRunStatus.Succeeded,
            StartedAtUtc = DateTime.UtcNow
        };

        await controller.DeleteWidgetAsync(widget);

        Assert.IsNull(controller.State.SaveError);
        Assert.AreEqual(1, repository.SaveCount);
        Assert.IsEmpty(controller.State.Dashboard!.Widgets);
        Assert.IsFalse(controller.State.WidgetResults.ContainsKey(widget.Id));
    }

    [TestMethod]
    public async Task SaveWidgetLayoutAsync_OverlappingLayout_SetsSaveErrorAndDoesNotSave()
    {
        var first = CreateWidget("widget-1") with
        {
            Layout = new DashboardLayout { X = 0, Y = 0, Width = 4, Height = 3, MinimumWidth = 1, MinimumHeight = 1 }
        };
        var second = CreateWidget("widget-2") with
        {
            Layout = new DashboardLayout { X = 5, Y = 0, Width = 4, Height = 3, MinimumWidth = 1, MinimumHeight = 1 }
        };

        var dashboard = CreateDashboard(widgets: [first, second]);
        var repository = new FakeDashboardRepository(dashboard);
        var controller = CreateController(repository);

        await controller.LoadAsync(dashboard.Id);

        await controller.SaveWidgetLayoutAsync(new DashboardWidgetLayoutChange(
            second.Id,
            second.Layout with { X = 3 }));

        Assert.AreEqual(0, repository.SaveCount);
        Assert.IsNotNull(controller.State.SaveError);
        Assert.Contains("layout overlaps widget", controller.State.SaveError);
        Assert.AreEqual(5, controller.State.Dashboard!.Widgets[1].Layout.X);
    }

    [TestMethod]
    public async Task BuildDashboardExport_LoadedDashboard_ReturnsFileNameAndJson()
    {
        var dashboard = CreateDashboard("Threat Hunting", widgets: []);
        var controller = CreateController(new FakeDashboardRepository(dashboard));

        await controller.LoadAsync(dashboard.Id);

        var exported = controller.BuildDashboardExport();

        Assert.IsNotNull(exported);
        Assert.StartsWith("threat-hunting-", exported.FileName);
        Assert.EndsWith(".json", exported.FileName);
        Assert.Contains("\"name\": \"Threat Hunting\"", exported.Json);
    }

    [TestMethod]
    public async Task Deactivate_RunningWidget_CancelsExecution()
    {
        var widget = CreateWidget("widget-1");
        var dashboard = CreateDashboard(widgets: []);
        var repository = new FakeDashboardRepository(dashboard);
        var renderedRunner = new BlockingRenderedQueryRunner();
        var controller = CreateController(repository, renderedRunner);

        await controller.LoadAsync(dashboard.Id);

        var runTask = controller.RunWidgetAsync(widget);
        Assert.IsTrue(await renderedRunner.WaitUntilStartedAsync(TestContext.CancellationToken));

        controller.Deactivate();
        await runTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        Assert.IsTrue(renderedRunner.CancellationObserved);
    }

    private static DashboardPageController CreateController(
        FakeDashboardRepository repository,
        IRenderedQueryRunner? renderedQueryRunner = null)
    {
        var runner = new DashboardWidgetRunner(
            renderedQueryRunner ?? new FakeRenderedQueryRunner(CreateRenderedResult(CreateSuccessfulQueryResult(), CreateTableFallbackChart())),
            new EChartsRenderOptionsBuilder(),
            new FakeSavedQueryRepository(),
            new FakeVisualizationRepository());

        return new DashboardPageController(
            repository,
            runner,
            new TestLogger<DashboardPageController>());
    }

    private static DashboardDefinition CreateDashboard(string name = "Dashboard", IReadOnlyList<DashboardWidgetDefinition>? widgets = null)
        => new()
        {
            Id = "dashboard-1",
            Name = name,
            Refresh = DashboardRefreshPolicy.Manual(),
            Widgets = widgets ?? [],
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

    private static DashboardWidgetDefinition CreateWidget(string id)
        => new()
        {
            Id = id,
            Title = id,
            Kind = DashboardWidgetKind.Query,
            QueryText = "ProcessEvent | take 10",
            Layout = new DashboardLayout
            {
                X = 0,
                Y = 0,
                Width = 4,
                Height = 3,
                MinimumWidth = 1,
                MinimumHeight = 1
            },
            Refresh = DashboardRefreshPolicy.Manual()
        };

    private static RenderedQueryResult CreateRenderedResult(
        QueryResult queryResult,
        RenderChartModel chart)
        => new(
            queryResult,
            RenderDirective.Table("No render directive."),
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

    private sealed class FakeDashboardRepository : IDashboardRepository
    {
        private readonly Dictionary<string, DashboardDefinition> _dashboards = new(StringComparer.OrdinalIgnoreCase);

        public FakeDashboardRepository(params DashboardDefinition[] dashboards)
        {
            foreach (var dashboard in dashboards)
            {
                _dashboards[dashboard.Id] = dashboard;
            }
        }

        public int SaveCount { get; private set; }

        public Task<IReadOnlyList<DashboardSummary>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DashboardSummary>>(
                _dashboards.Values
                    .Select(dashboard => new DashboardSummary
                    {
                        Name = dashboard.Name,
                        Description = dashboard.Description,
                        WidgetCount = dashboard.Widgets.Count
                    })
                    .ToArray());

        public Task<DashboardDefinition?> GetAsync(string id, CancellationToken ct = default)
        {
            _dashboards.TryGetValue(id, out var dashboard);
            return Task.FromResult(dashboard);
        }

        public Task SaveAsync(DashboardDefinition dashboard, CancellationToken ct = default)
        {
            SaveCount++;
            _dashboards[dashboard.Id] = dashboard;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id, CancellationToken ct = default)
        {
            _dashboards.Remove(id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRenderedQueryRunner : IRenderedQueryRunner
    {
        private readonly RenderedQueryResult _result;

        public FakeRenderedQueryRunner(RenderedQueryResult result)
        {
            _result = result;
        }

        public int RunCount { get; private set; }

        public string? LastQueryText { get; private set; }

        public RenderDirective? LastDirective { get; private set; }

        public Task<RenderedQueryResult> RunAsync(
            string queryText,
            CancellationToken cancellationToken = default)
        {
            RunCount++;
            LastQueryText = queryText;
            LastDirective = null;
            return Task.FromResult(_result);
        }

        public Task<RenderedQueryResult> RunAsync(
            string queryText,
            RenderDirective directive,
            CancellationToken cancellationToken = default)
        {
            RunCount++;
            LastQueryText = queryText;
            LastDirective = directive;
            return Task.FromResult(_result);
        }
    }

    private sealed class BlockingRenderedQueryRunner : IRenderedQueryRunner
    {
        private readonly TaskCompletionSource _started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool CancellationObserved { get; private set; }

        public Task<RenderedQueryResult> RunAsync(
            string queryText,
            CancellationToken cancellationToken = default)
            => RunBlockingAsync(cancellationToken);

        public Task<RenderedQueryResult> RunAsync(
            string queryText,
            RenderDirective directive,
            CancellationToken cancellationToken = default)
            => RunBlockingAsync(cancellationToken);

        private async Task<RenderedQueryResult> RunBlockingAsync(CancellationToken cancellationToken)
        {
            _started.TrySetResult();

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                CancellationObserved = true;
                throw;
            }

            throw new UnreachableException("The blocking runner should only finish through cancellation.");
        }

        public async Task<bool> WaitUntilStartedAsync(CancellationToken cancellationToken)
        {
            var completed = await Task.WhenAny(_started.Task, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
            return completed == _started.Task;
        }
    }

    private sealed class FakeSavedQueryRepository : ISavedQueryRepository
    {
        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<SavedQueryRecord>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SavedQueryRecord>>([]);

        public Task<SavedQueryRecord?> GetAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult<SavedQueryRecord?>(null);

        public Task SaveAsync(SavedQueryRecord query, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarkRunAsync(string id, DateTime runAt, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeVisualizationRepository : IVisualizationRepository
    {
        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<VisualizationRecord>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VisualizationRecord>>([]);

        public Task<IReadOnlyList<VisualizationRecord>> ListByQueryAsync(string queryId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VisualizationRecord>>([]);

        public Task<VisualizationRecord?> GetAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult<VisualizationRecord?>(null);

        public Task SaveAsync(VisualizationRecord visualization, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }

    public TestContext TestContext { get; set; }
}
