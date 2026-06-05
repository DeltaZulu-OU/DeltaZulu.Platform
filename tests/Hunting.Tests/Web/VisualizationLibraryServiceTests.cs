namespace Hunting.Tests.Web;

using System.Text.Json;
using Hunting.Application.SavedQueries;
using Hunting.Application.Visualizations;
using Hunting.Render.Model;
using Hunting.Web.Services;

[TestClass]
public sealed class VisualizationLibraryServiceTests
{
    [TestMethod]
    public async Task SaveVisualizationAsync_PersistsVisualizationForExistingSavedQuery()
    {
        var savedQueries = new InMemorySavedQueryRepository();
        var visualizations = new InMemoryVisualizationRepository();
        var service = new VisualizationLibraryService(savedQueries, visualizations);

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
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

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
    public async Task SaveVisualizationAsync_UpdatesExistingVisualizationAndPreservesCreatedAt()
    {
        var savedQueries = new InMemorySavedQueryRepository();
        var visualizations = new InMemoryVisualizationRepository();
        var service = new VisualizationLibraryService(savedQueries, visualizations);

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
        Assert.IsTrue(updated.UpdatedAt >= createdAt);
        Assert.AreEqual("Updated", updated.Name);
        Assert.AreEqual(nameof(RenderKind.Piechart), updated.Kind);
    }

    [TestMethod]
    public async Task SaveVisualizationAsync_MissingSavedQuery_Throws()
    {
        var service = new VisualizationLibraryService(
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
        var service = new VisualizationLibraryService(savedQueries, visualizations);

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

    public TestContext TestContext { get; set; }
}
