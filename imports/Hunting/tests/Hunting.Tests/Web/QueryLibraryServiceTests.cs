namespace Hunting.Tests.Web;

using Hunting.Application.QueryHistory;
using Hunting.Application.SavedQueries;
using Hunting.Application.Visualizations;
using Hunting.Web.Services;

[TestClass]
public sealed class QueryLibraryServiceTests
{
    [TestMethod]
    public async Task DeleteSavedQueryAsync_UnusedQuery_DeletesRecord()
    {
        var savedQueries = new InMemorySavedQueryRepository();
        var service = CreateService(savedQueries: savedQueries);
        var now = DateTime.UtcNow;

        await savedQueries.SaveAsync(new SavedQueryRecord(
            "query-1",
            "Unused",
            null,
            "ProcessEvent | take 10",
            now,
            now,
            null), TestContext.CancellationToken);

        await service.DeleteSavedQueryAsync("query-1", TestContext.CancellationToken);

        Assert.IsNull(await savedQueries.GetAsync("query-1", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task DeleteSavedQueryAsync_QueryUsedByVisualization_ThrowsAndKeepsRecord()
    {
        var savedQueries = new InMemorySavedQueryRepository();
        var visualizations = new InMemoryVisualizationRepository();
        var service = CreateService(savedQueries: savedQueries, visualizations: visualizations);
        var now = DateTime.UtcNow;

        await savedQueries.SaveAsync(new SavedQueryRecord(
            "query-1",
            "Used query",
            null,
            "ProcessEvent | take 10",
            now,
            now,
            null), TestContext.CancellationToken);

        await visualizations.SaveAsync(new VisualizationRecord(
            "viz-1",
            "query-1",
            "Events by account",
            null,
            "Barchart",
            "{}",
            now,
            now), TestContext.CancellationToken);

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.DeleteSavedQueryAsync("query-1", TestContext.CancellationToken));

        Assert.Contains("Events by account", ex.Message);
        Assert.IsNotNull(await savedQueries.GetAsync("query-1", TestContext.CancellationToken));
    }

    private static QueryLibraryService CreateService(
        InMemorySavedQueryRepository? savedQueries = null,
        InMemoryQueryHistoryRepository? queryHistory = null,
        InMemoryVisualizationRepository? visualizations = null)
        => new(
            savedQueries ?? new InMemorySavedQueryRepository(),
            queryHistory ?? new InMemoryQueryHistoryRepository(),
            visualizations ?? new InMemoryVisualizationRepository());

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
            => Task.FromResult<IReadOnlyList<VisualizationRecord>>(_records.Values.ToArray());

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
