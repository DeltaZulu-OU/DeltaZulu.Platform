namespace DeltaZulu.Hunting.Tests.Web;

using DeltaZulu.Hunting.Application.QueryHistory;
using DeltaZulu.Hunting.Application.SavedQueries;
using DeltaZulu.Hunting.Application.Visualizations;
using DeltaZulu.Hunting.Render.Directives;
using DeltaZulu.Hunting.Render.Model;
using DeltaZulu.Hunting.Web.Dashboards;
using DeltaZulu.Hunting.Web.Dashboards.Persistence;
using DeltaZulu.Hunting.Web.Services;

[TestClass]
public sealed class LibraryServiceTests
{
    [TestMethod]
    public async Task ListAsync_ReturnsSavedQueryVisualizationAndDashboardItems()
    {
        var now = new DateTime(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);
        var savedQueries = new InMemorySavedQueryRepository();
        var visualizations = new InMemoryVisualizationRepository();
        var dashboards = new InMemoryDashboardRepository();

        await savedQueries.SaveAsync(new SavedQueryRecord(
            "query-1",
            "PowerShell search",
            "Process launches",
            "ProcessEvent | take 10",
            now.AddMinutes(-3),
            now.AddMinutes(-2),
            null), TestContext.CancellationToken);

        await visualizations.SaveAsync(new VisualizationRecord(
            "viz-1",
            "query-1",
            "PowerShell chart",
            "Launches by account",
            "Barchart",
            "{}",
            now.AddMinutes(-2),
            now.AddMinutes(-1)), TestContext.CancellationToken);

        await dashboards.SaveAsync(new DashboardDefinition
        {
            Id = "dashboard-1",
            Name = "SOC overview",
            Description = "Daily view",
            Widgets =
            [
                new DashboardWidgetDefinition
                {
                    Id = "widget-1",
                    Title = "Widget",
                    QueryText = "ProcessEvent | take 10"
                }
            ],
            CreatedAtUtc = now.AddMinutes(-1),
            UpdatedAtUtc = now
        }, TestContext.CancellationToken);

        var service = CreateService(savedQueries, visualizations, dashboards);

        var items = await service.ListAsync(TestContext.CancellationToken);

        Assert.HasCount(3, items);

        var queryItem = items.Single(item => item.Kind == LibraryItemKind.SavedQuery);
        Assert.AreEqual("query-1", queryItem.Id);
        Assert.AreEqual("PowerShell search", queryItem.Name);
        Assert.AreEqual("1 saved visualization(s)", queryItem.DependencyLabel);
        Assert.AreEqual(LibraryItemStatus.Ok, queryItem.Status);

        var visualizationItem = items.Single(item => item.Kind == LibraryItemKind.Visualization);
        Assert.AreEqual("viz-1", visualizationItem.Id);
        Assert.AreEqual("PowerShell chart", visualizationItem.Name);
        Assert.AreEqual("Query: PowerShell search", visualizationItem.DependencyLabel);
        Assert.AreEqual(LibraryItemStatus.Ok, visualizationItem.Status);

        var dashboardItem = items.Single(item => item.Kind == LibraryItemKind.Dashboard);
        Assert.AreEqual("dashboard-1", dashboardItem.Id);
        Assert.AreEqual("SOC overview", dashboardItem.Name);
        Assert.AreEqual("1 widget(s)", dashboardItem.DependencyLabel);
        Assert.AreEqual(LibraryItemStatus.Ok, dashboardItem.Status);
    }

    [TestMethod]
    public async Task ListAsync_VisualizationWithMissingQuery_ReturnsMissingDependencyStatus()
    {
        var now = new DateTime(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);
        var visualizations = new InMemoryVisualizationRepository();

        await visualizations.SaveAsync(new VisualizationRecord(
            "viz-1",
            "missing-query-id",
            "Broken chart",
            null,
            "Barchart",
            "{}",
            now,
            now), TestContext.CancellationToken);

        var service = CreateService(visualizations: visualizations);

        var item = (await service.ListAsync(TestContext.CancellationToken)).Single();

        Assert.AreEqual(LibraryItemKind.Visualization, item.Kind);
        Assert.AreEqual(LibraryItemStatus.MissingDependency, item.Status);
        Assert.Contains("Missing query", item.DependencyLabel);
    }

    [TestMethod]
    public async Task DeleteAsync_SavedQuery_UsesProtectedQueryDeletePath()
    {
        var now = new DateTime(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);
        var savedQueries = new InMemorySavedQueryRepository();
        var visualizations = new InMemoryVisualizationRepository();

        await savedQueries.SaveAsync(new SavedQueryRecord(
            "query-1",
            "Used search",
            null,
            "ProcessEvent | take 10",
            now,
            now,
            null), TestContext.CancellationToken);

        await visualizations.SaveAsync(new VisualizationRecord(
            "viz-1",
            "query-1",
            "Dependent chart",
            null,
            "Barchart",
            "{}",
            now,
            now), TestContext.CancellationToken);

        var service = CreateService(savedQueries, visualizations);

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.DeleteAsync(
                new LibraryItem(
                    "query-1",
                    LibraryItemKind.SavedQuery,
                    "Used search",
                    null,
                    "1 saved visualization(s)",
                    now,
                    LibraryItemStatus.Ok),
                TestContext.CancellationToken));

        Assert.Contains("Dependent chart", ex.Message);
        Assert.IsNotNull(await savedQueries.GetAsync("query-1", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task DeleteAsync_Dashboard_DeletesDashboard()
    {
        var now = new DateTime(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);
        var dashboards = new InMemoryDashboardRepository();

        await dashboards.SaveAsync(new DashboardDefinition
        {
            Id = "dashboard-1",
            Name = "SOC overview",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }, TestContext.CancellationToken);

        var service = CreateService(dashboards: dashboards);

        await service.DeleteAsync("dashboard-1", LibraryItemKind.Dashboard, TestContext.CancellationToken);

        Assert.IsNull(await dashboards.GetAsync("dashboard-1", TestContext.CancellationToken));
    }

    private static LibraryService CreateService(
        InMemorySavedQueryRepository? savedQueries = null,
        InMemoryVisualizationRepository? visualizations = null,
        InMemoryDashboardRepository? dashboards = null)
    {
        savedQueries ??= new InMemorySavedQueryRepository();
        visualizations ??= new InMemoryVisualizationRepository();
        dashboards ??= new InMemoryDashboardRepository();

        var queryLibrary = new QueryLibraryService(
            savedQueries,
            new InMemoryQueryHistoryRepository(),
            visualizations);

        var visualizationLibrary = new VisualizationLibraryService(
            savedQueries,
            visualizations,
            new FakeRenderDirectiveParser(),
            dashboards);

        return new LibraryService(queryLibrary, visualizationLibrary, dashboards);
    }

    private sealed class InMemorySavedQueryRepository : ISavedQueryRepository
    {
        private readonly Dictionary<string, SavedQueryRecord> _records = new(StringComparer.OrdinalIgnoreCase);

        public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            _records.Remove(id);
            return Task.CompletedTask;
        }

        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<SavedQueryRecord?> GetAsync(string id, CancellationToken cancellationToken = default)
        {
            _records.TryGetValue(id, out var record);
            return Task.FromResult(record);
        }

        public Task<IReadOnlyList<SavedQueryRecord>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SavedQueryRecord>>(
                _records.Values
                    .OrderByDescending(record => record.UpdatedAt)
                    .ThenBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray());

        public Task MarkRunAsync(string id, DateTime runAt, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveAsync(SavedQueryRecord query, CancellationToken cancellationToken = default)
        {
            _records[query.Id] = query;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryVisualizationRepository : IVisualizationRepository
    {
        private readonly Dictionary<string, VisualizationRecord> _records = new(StringComparer.OrdinalIgnoreCase);

        public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            _records.Remove(id);
            return Task.CompletedTask;
        }

        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<VisualizationRecord?> GetAsync(string id, CancellationToken cancellationToken = default)
        {
            _records.TryGetValue(id, out var record);
            return Task.FromResult(record);
        }

        public Task<IReadOnlyList<VisualizationRecord>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VisualizationRecord>>(
                _records.Values
                    .OrderByDescending(record => record.UpdatedAt)
                    .ThenBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray());

        public Task<IReadOnlyList<VisualizationRecord>> ListByQueryAsync(
            string queryId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VisualizationRecord>>(
                _records.Values
                    .Where(record => string.Equals(record.QueryId, queryId, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(record => record.UpdatedAt)
                    .ThenBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray());

        public Task SaveAsync(VisualizationRecord visualization, CancellationToken cancellationToken = default)
        {
            _records[visualization.Id] = visualization;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryQueryHistoryRepository : IQueryHistoryRepository
    {
        public Task AddAsync(QueryHistoryRecord record, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ClearAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<QueryHistoryRecord>> ListRecentAsync(
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<QueryHistoryRecord>>([]);
    }

    private sealed class InMemoryDashboardRepository : IDashboardRepository
    {
        private readonly Dictionary<string, DashboardDefinition> _dashboards = new(StringComparer.OrdinalIgnoreCase);

        public Task DeleteAsync(string id, CancellationToken ct = default)
        {
            _dashboards.Remove(id);
            return Task.CompletedTask;
        }

        public Task<DashboardDefinition?> GetAsync(string id, CancellationToken ct = default)
        {
            _dashboards.TryGetValue(id, out var dashboard);
            return Task.FromResult(dashboard);
        }

        public Task<IReadOnlyList<DashboardSummary>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DashboardSummary>>(
                _dashboards.Values
                    .Select(dashboard => new DashboardSummary
                    {
                        Id = dashboard.Id,
                        Name = dashboard.Name,
                        Description = dashboard.Description,
                        WidgetCount = dashboard.Widgets.Count,
                        UpdatedAtUtc = dashboard.UpdatedAtUtc
                    })
                    .OrderByDescending(summary => summary.UpdatedAtUtc)
                    .ToArray());

        public Task SaveAsync(DashboardDefinition dashboard, CancellationToken ct = default)
        {
            _dashboards[dashboard.Id] = dashboard;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRenderDirectiveParser : IRenderDirectiveParser
    {
        public RenderDirectiveParseResult Parse(string queryText)
            => new()
            {
                QueryTextWithoutRender = queryText,
                Directive = RenderDirective.Table()
            };
    }

    public TestContext TestContext { get; set; }
}