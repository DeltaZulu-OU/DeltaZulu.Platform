namespace DeltaZulu.Hunting.Tests;

using DeltaZulu.Hunting.Application.Visualizations;
using DeltaZulu.Hunting.Data.Persistence;
using DeltaZulu.Hunting.Data.Visualizations;
using Microsoft.Data.Sqlite;

[TestClass]
public sealed class VisualizationRepositoryTests
{
    [TestMethod]
    public async Task ListAsync_ReturnsEmptyList_WhenStoreIsEmpty()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var visualizations = await repository.ListAsync(TestContext.CancellationToken);

            Assert.IsEmpty(visualizations);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_PersistsVisualizationAcrossRepositoryInstances()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var createdAt = new DateTime(2026, 6, 5, 8, 0, 0, DateTimeKind.Utc);
            var updatedAt = new DateTime(2026, 6, 5, 8, 5, 0, DateTimeKind.Utc);

            await repository.SaveAsync(new VisualizationRecord(
                "visualization-1",
                "query-1",
                "PowerShell by device",
                "Chart for recent PowerShell launches.",
                "barchart",
                "{\"xcolumn\":\"DeviceName\",\"ycolumns\":[\"LaunchCount\"]}",
                createdAt,
                updatedAt), TestContext.CancellationToken);

            var secondRepository = new DapperVisualizationRepository(
                new SqliteAppDbConnectionFactory(BuildConnectionString(dbPath)));

            var saved = await secondRepository.GetAsync("visualization-1", TestContext.CancellationToken);

            Assert.IsNotNull(saved);
            Assert.AreEqual("query-1", saved.QueryId);
            Assert.AreEqual("PowerShell by device", saved.Name);
            Assert.AreEqual("Chart for recent PowerShell launches.", saved.Description);
            Assert.AreEqual("barchart", saved.Kind);
            Assert.AreEqual("{\"xcolumn\":\"DeviceName\",\"ycolumns\":[\"LaunchCount\"]}", saved.SpecJson);
            Assert.AreEqual(createdAt, saved.CreatedAt);
            Assert.AreEqual(updatedAt, saved.UpdatedAt);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task ListByQueryAsync_ReturnsOnlyMatchingVisualizations()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 5, 8, 0, 0, DateTimeKind.Utc);

            await repository.SaveAsync(new VisualizationRecord(
                "visualization-1",
                "query-1",
                "Query 1 chart",
                null,
                "barchart",
                "{\"xcolumn\":\"DeviceName\"}",
                now,
                now), TestContext.CancellationToken);

            await repository.SaveAsync(new VisualizationRecord(
                "visualization-2",
                "query-2",
                "Query 2 chart",
                null,
                "timechart",
                "{\"xcolumn\":\"Timestamp\"}",
                now,
                now), TestContext.CancellationToken);

            var visualizations = await repository.ListByQueryAsync("query-1", TestContext.CancellationToken);

            Assert.HasCount(1, visualizations);
            Assert.AreEqual("visualization-1", visualizations[0].Id);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_UpdatesExistingVisualization()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var createdAt = new DateTime(2026, 6, 5, 8, 0, 0, DateTimeKind.Utc);
            var firstUpdatedAt = new DateTime(2026, 6, 5, 8, 5, 0, DateTimeKind.Utc);
            var secondUpdatedAt = new DateTime(2026, 6, 5, 8, 10, 0, DateTimeKind.Utc);

            await repository.SaveAsync(new VisualizationRecord(
                "visualization-1",
                "query-1",
                "Original",
                null,
                "barchart",
                "{\"xcolumn\":\"DeviceName\"}",
                createdAt,
                firstUpdatedAt), TestContext.CancellationToken);

            await repository.SaveAsync(new VisualizationRecord(
                "visualization-1",
                "query-1",
                "Updated",
                "Updated description",
                "columnchart",
                "{\"xcolumn\":\"DeviceName\",\"stacked\":true}",
                createdAt,
                secondUpdatedAt), TestContext.CancellationToken);

            var saved = await repository.GetAsync("visualization-1", TestContext.CancellationToken);

            Assert.IsNotNull(saved);
            Assert.AreEqual("Updated", saved.Name);
            Assert.AreEqual("Updated description", saved.Description);
            Assert.AreEqual("columnchart", saved.Kind);
            Assert.AreEqual("{\"xcolumn\":\"DeviceName\",\"stacked\":true}", saved.SpecJson);
            Assert.AreEqual(createdAt, saved.CreatedAt);
            Assert.AreEqual(secondUpdatedAt, saved.UpdatedAt);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task DeleteAsync_RemovesVisualization()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 5, 8, 0, 0, DateTimeKind.Utc);

            await repository.SaveAsync(new VisualizationRecord(
                "visualization-1",
                "query-1",
                "Chart",
                null,
                "barchart",
                "{\"xcolumn\":\"DeviceName\"}",
                now,
                now), TestContext.CancellationToken);

            await repository.DeleteAsync("visualization-1", TestContext.CancellationToken);

            var saved = await repository.GetAsync("visualization-1", TestContext.CancellationToken);

            Assert.IsNull(saved);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    private static DapperVisualizationRepository CreateRepository(out string dbPath)
    {
        dbPath = Path.Combine(
            Path.GetTempPath(),
            $"hunting-visualizations-{Guid.NewGuid():N}.db");

        var connectionFactory = new SqliteAppDbConnectionFactory(BuildConnectionString(dbPath));
        return new DapperVisualizationRepository(connectionFactory);
    }

    private static string BuildConnectionString(string dbPath) => $"Data Source={dbPath};Pooling=False";

    private static void DeleteDatabaseFiles(string path)
    {
        SqliteConnection.ClearAllPools();

        DeleteIfExists(path);
        DeleteIfExists($"{path}-wal");
        DeleteIfExists($"{path}-shm");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public TestContext TestContext { get; set; }
}