namespace DeltaZulu.Platform.Tests.Hunting.Web;

using System.Text.Json;
using DeltaZulu.Platform.Application.Hunting.Render.Directives;
using DeltaZulu.Platform.Domain.Hunting.Rendering;
using DeltaZulu.Platform.Domain.Hunting.SavedQueries;
using DeltaZulu.Platform.Domain.Hunting.Visualizations;
using DeltaZulu.Platform.Web.Hunting.Dashboards;
using DeltaZulu.Platform.Web.Hunting.Dashboards.Persistence;
using DeltaZulu.Platform.Web.Hunting.Services;

[TestClass]
public sealed class VisualizationLibraryServiceTests
{
    private static readonly JsonSerializerOptions opt = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    [TestMethod]
    public async Task SaveVisualizationAsync_PersistsVisualizationForExistingSavedQuery()
    {
        var savedQueries = new InMemorySavedQueryRepository();
        var visualizations = new InMemoryVisualizationRepository();
        var service = CreateService(savedQueries, visualizations);

        var now = new DateTime(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);
        await savedQueries.SaveAsync(new SavedQueryRecord(
            "query-1",
            "PowerShell launches",
            null,
            "ProcessEvent | summarize LaunchCount = count() by AccountName",
            now,
            now,
            null), TestContext.CancellationToken);

        var record = await service.SaveVisualizationAsync(
            id: null,
            queryId: " query-1 ",
            name: " Launches by account ",
            description: " Chart for account launch counts ",
            kind: RenderKind.Barchart,
            spec: new VisualizationSpec
            {
                Title = " Launches by account ",
                XColumn = " AccountName ",
                YColumns = [" LaunchCount ", "LaunchCount", " "],
                SeriesColumn = " ",
                Legend = " auto ",
                IsStacked = true
            },
            TestContext.CancellationToken);

        Assert.IsFalse(string.IsNullOrWhiteSpace(record.Id));
        Assert.AreEqual("query-1", record.QueryId);
        Assert.AreEqual("Launches by account", record.Name);
        Assert.AreEqual("Chart for account launch counts", record.Description);
        Assert.AreEqual(nameof(RenderKind.Barchart), record.Kind);
        Assert.AreEqual(record, await visualizations.GetAsync(record.Id, TestContext.CancellationToken));

        var spec = JsonSerializer.Deserialize<VisualizationSpec>(
            record.SpecJson,
            opt);

        Assert.IsNotNull(spec);
        Assert.AreEqual("Launches by account", spec.Title);
        Assert.AreEqual("AccountName", spec.XColumn);
        Assert.HasCount(1, spec.YColumns);
        Assert.AreEqual("LaunchCount", spec.YColumns[0]);
        Assert.IsNull(spec.SeriesColumn);
        Assert.AreEqual("auto", spec.Legend);
        Assert.IsTrue(spec.IsStacked);
    }

    [TestMethod]
    public async Task SaveVisualizationFromRenderedQueryAsync_SavesQueryWithoutRenderAndVisualizationSpec()
    {
        var savedQueries = new InMemorySavedQueryRepository();
        var visualizations = new InMemoryVisualizationRepository();
        var service = CreateService(savedQueries, visualizations);

        var result = await service.SaveVisualizationFromRenderedQueryAsync(
            queryId: null,
            visualizationId: null,
            name: "Events by account",
            description: "Dashboard chart.",
            queryText:
            """
            ProcessEvent
            | summarize Count = count() by AccountName
            | render barchart xcolumn=AccountName ycolumns=Count title='Events by account'
            """,
            TestContext.CancellationToken);

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Query.Id));
        Assert.AreEqual(result.Query.Id, result.Visualization.QueryId);
        Assert.DoesNotContain("| render", result.Query.QueryText, StringComparison.OrdinalIgnoreCase);
        Assert.AreEqual("Events by account", result.Query.Name);
        Assert.AreEqual("Events by account", result.Visualization.Name);
        Assert.AreEqual(nameof(RenderKind.Barchart), result.Visualization.Kind);

        var spec = JsonSerializer.Deserialize<VisualizationSpec>(
            result.Visualization.SpecJson,
            opt);

        Assert.IsNotNull(spec);
        Assert.AreEqual("Events by account", spec.Title);
        Assert.AreEqual("AccountName", spec.XColumn);
        Assert.HasCount(1, spec.YColumns);
        Assert.AreEqual("Count", spec.YColumns[0]);
    }

    [TestMethod]
    public async Task LoadVisualizationQueryTextAsync_ReconstructsSavedQueryWithRenderClause()
    {
        var savedQueries = new InMemorySavedQueryRepository();
        var visualizations = new InMemoryVisualizationRepository();
        var service = CreateService(savedQueries, visualizations);
        var now = new DateTime(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

        await savedQueries.SaveAsync(new SavedQueryRecord(
            "query-1",
            "Events by account",
            null,
            """
            ProcessEvent
            | summarize Count = count() by AccountName
            """,
            now,
            now,
            null), TestContext.CancellationToken);

        await visualizations.SaveAsync(new VisualizationRecord(
            "viz-1",
            "query-1",
            "Events by account",
            null,
            nameof(RenderKind.Barchart),
            JsonSerializer.Serialize(
                new VisualizationSpec
                {
                    Title = "Events by account",
                    XColumn = "AccountName",
                    YColumns = ["Count"],
                    IsStacked = true
                },
                opt),
            now,
            now), TestContext.CancellationToken);

        var queryText = await service.LoadVisualizationQueryTextAsync("viz-1", TestContext.CancellationToken);

        Assert.IsNotNull(queryText);
        Assert.Contains("ProcessEvent", queryText);
        Assert.Contains("| render barchart", queryText);
        Assert.Contains("title='Events by account'", queryText);
        Assert.Contains("xcolumn='AccountName'", queryText);
        Assert.Contains("ycolumns='Count'", queryText);
        Assert.Contains("kind='stacked'", queryText);
    }

    [TestMethod]
    public async Task LoadVisualizationQueryTextAsync_MissingVisualization_ReturnsNull()
    {
        var service = CreateService(
            new InMemorySavedQueryRepository(),
            new InMemoryVisualizationRepository());

        var queryText = await service.LoadVisualizationQueryTextAsync("missing", TestContext.CancellationToken);

        Assert.IsNull(queryText);
    }

    [TestMethod]
    public async Task SaveVisualizationFromRenderedQueryAsync_QueryWithoutRender_Throws()
    {
        var service = CreateService(
            new InMemorySavedQueryRepository(),
            new InMemoryVisualizationRepository());

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.SaveVisualizationFromRenderedQueryAsync(
                null,
                null,
                "No render",
                null,
                "ProcessEvent | take 10",
                TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task SaveVisualizationAsync_UpdatesExistingVisualizationAndPreservesCreatedAt()
    {
        var savedQueries = new InMemorySavedQueryRepository();
        var visualizations = new InMemoryVisualizationRepository();
        var service = CreateService(savedQueries, visualizations);

        var createdAt = new DateTime(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);
        await savedQueries.SaveAsync(new SavedQueryRecord(
            "query-1",
            "Query",
            null,
            "ProcessEvent | take 10",
            createdAt,
            createdAt,
            null), TestContext.CancellationToken);

        await visualizations.SaveAsync(new VisualizationRecord(
            "viz-1",
            "query-1",
            "Original",
            null,
            nameof(RenderKind.Table),
            "{}",
            createdAt,
            createdAt), TestContext.CancellationToken);

        var updated = await service.SaveVisualizationAsync(
            "viz-1",
            "query-1",
            "Updated",
            null,
            RenderKind.Piechart,
            new VisualizationSpec { XColumn = "FileName", YColumns = ["Count"] },
            TestContext.CancellationToken);

        Assert.AreEqual(createdAt, updated.CreatedAt);
        Assert.IsGreaterThanOrEqualTo(createdAt, updated.UpdatedAt);
        Assert.AreEqual("Updated", updated.Name);
        Assert.AreEqual(nameof(RenderKind.Piechart), updated.Kind);
    }

    [TestMethod]
    public async Task SaveVisualizationAsync_MissingSavedQuery_Throws()
    {
        var service = CreateService(
            new InMemorySavedQueryRepository(),
            new InMemoryVisualizationRepository());

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.SaveVisualizationAsync(
                null,
                "missing-query",
                "Visualization",
                null,
                RenderKind.Barchart,
                new VisualizationSpec { XColumn = "AccountName", YColumns = ["LaunchCount"] },
                TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ListVisualizationsByQueryAsync_DelegatesToRepository()
    {
        var savedQueries = new InMemorySavedQueryRepository();
        var visualizations = new InMemoryVisualizationRepository();
        var service = CreateService(savedQueries, visualizations);

        var now = DateTime.UtcNow;
        await visualizations.SaveAsync(new VisualizationRecord(
            "viz-1",
            "query-1",
            "First",
            null,
            nameof(RenderKind.Table),
            "{}",
            now,
            now), TestContext.CancellationToken);
        await visualizations.SaveAsync(new VisualizationRecord(
            "viz-2",
            "query-2",
            "Second",
            null,
            nameof(RenderKind.Table),
            "{}",
            now,
            now), TestContext.CancellationToken);

        var result = await service.ListVisualizationsByQueryAsync("query-1", TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.AreEqual("viz-1", result[0].Id);
    }

    [TestMethod]
    public async Task DeleteVisualizationAsync_UnusedVisualization_DeletesRecord()
    {
        var visualizations = new InMemoryVisualizationRepository();
        var service = CreateService(new InMemorySavedQueryRepository(), visualizations);
        var now = DateTime.UtcNow;
        await visualizations.SaveAsync(new VisualizationRecord(
            "viz-1",
            "query-1",
            "Visualization",
            null,
            nameof(RenderKind.Barchart),
            "{}",
            now,
            now), TestContext.CancellationToken);

        await service.DeleteVisualizationAsync("viz-1", TestContext.CancellationToken);

        Assert.IsNull(await visualizations.GetAsync("viz-1", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task DeleteVisualizationAsync_UsedByDashboard_ThrowsAndKeepsRecord()
    {
        var visualizations = new InMemoryVisualizationRepository();
        var dashboards = new InMemoryDashboardRepository(new DashboardDefinition
        {
            Id = "dashboard-1",
            Name = "SOC Overview",
            Widgets =
            [
                new DashboardWidgetDefinition
                {
                    Id = "widget-1",
                    Title = "Events by account",
                    VisualizationId = "viz-1",
                    QueryText = string.Empty
                }
            ],
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        var service = CreateService(new InMemorySavedQueryRepository(), visualizations, dashboards);
        var now = DateTime.UtcNow;
        await visualizations.SaveAsync(new VisualizationRecord(
            "viz-1",
            "query-1",
            "Visualization",
            null,
            nameof(RenderKind.Barchart),
            "{}",
            now,
            now), TestContext.CancellationToken);

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.DeleteVisualizationAsync("viz-1", TestContext.CancellationToken));

        Assert.Contains("SOC Overview", ex.Message);
        Assert.IsNotNull(await visualizations.GetAsync("viz-1", TestContext.CancellationToken));
    }

    private static VisualizationLibraryService CreateService(
        ISavedQueryRepository savedQueries,
        IVisualizationRepository visualizations,
        IDashboardRepository? dashboards = null)
        => new(savedQueries, visualizations, new RenderDirectiveParser(), dashboards ?? new InMemoryDashboardRepository());

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
            => Task.FromResult<IReadOnlyList<SavedQueryRecord>>(_records.Values.ToArray());

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

    private sealed class InMemoryDashboardRepository : IDashboardRepository
    {
        private readonly Dictionary<string, DashboardDefinition> _dashboards = new(StringComparer.OrdinalIgnoreCase);

        public InMemoryDashboardRepository(params DashboardDefinition[] dashboards)
        {
            foreach (var dashboard in dashboards)
            {
                _dashboards[dashboard.Id] = dashboard;
            }
        }

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
                        CreatedAtUtc = dashboard.CreatedAtUtc,
                        UpdatedAtUtc = dashboard.UpdatedAtUtc
                    })
                    .ToArray());

        public Task SaveAsync(DashboardDefinition dashboard, CancellationToken ct = default)
        {
            _dashboards[dashboard.Id] = dashboard;
            return Task.CompletedTask;
        }
    }

    public TestContext TestContext { get; set; }
}