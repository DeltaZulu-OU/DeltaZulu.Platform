namespace Hunting.Tests;

using Hunting.Data.Persistence;
using Hunting.Data.QueryHistory;
using Microsoft.Data.Sqlite;
using AppQueryHistoryRecord = Hunting.Application.QueryHistory.QueryHistoryRecord;

[TestClass]
public sealed class QueryHistoryRepositoryTests
{
    [TestMethod]
    public async Task ListRecentAsync_ReturnsEmptyList_WhenStoreIsEmpty()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var history = await repository.ListRecentAsync(cancellationToken: TestContext.CancellationToken);

            Assert.IsEmpty(history);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task AddAsync_PersistsQueryHistoryAcrossRepositoryInstances()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var executedAt = new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc);

            await repository.AddAsync(new AppQueryHistoryRecord(
                "history-1",
                "ProcessEvent | take 10",
                executedAt,
                true,
                10,
                42,
                null), TestContext.CancellationToken);

            var secondRepository = new DapperQueryHistoryRepository(
                new SqliteAppDbConnectionFactory(BuildConnectionString(dbPath)));

            var history = await secondRepository.ListRecentAsync(cancellationToken: TestContext.CancellationToken);

            Assert.HasCount(1, history);
            Assert.AreEqual("history-1", history[0].Id);
            Assert.AreEqual("ProcessEvent | take 10", history[0].QueryText);
            Assert.AreEqual(executedAt, history[0].ExecutedAt);
            Assert.IsTrue(history[0].Succeeded);
            Assert.AreEqual(10, history[0].RowCount);
            Assert.AreEqual(42, history[0].DurationMs);
            Assert.IsNull(history[0].DiagnosticSummary);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task ListRecentAsync_OrdersNewestFirstAndHonorsLimit()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            await repository.AddAsync(new AppQueryHistoryRecord(
                "old",
                "ProcessEvent | take 1",
                new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc),
                true,
                1,
                10,
                null), TestContext.CancellationToken);

            await repository.AddAsync(new AppQueryHistoryRecord(
                "new",
                "ProcessEvent | take 2",
                new DateTime(2026, 6, 3, 9, 0, 0, DateTimeKind.Utc),
                true,
                2,
                20,
                null), TestContext.CancellationToken);

            var history = await repository.ListRecentAsync(limit: 1, TestContext.CancellationToken);

            Assert.HasCount(1, history);
            Assert.AreEqual("new", history[0].Id);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task AddAsync_PersistsFailedQueryDiagnostics()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            await repository.AddAsync(new AppQueryHistoryRecord(
                "failed",
                "BadQuery",
                new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc),
                false,
                null,
                12,
                "The referenced table or column does not exist."), TestContext.CancellationToken);

            var history = await repository.ListRecentAsync(cancellationToken: TestContext.CancellationToken);

            Assert.HasCount(1, history);
            Assert.IsFalse(history[0].Succeeded);
            Assert.IsNull(history[0].RowCount);
            Assert.AreEqual("The referenced table or column does not exist.", history[0].DiagnosticSummary);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task ClearAsync_RemovesHistory()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            await repository.AddAsync(new AppQueryHistoryRecord(
                "history-1",
                "ProcessEvent | take 10",
                new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc),
                true,
                10,
                42,
                null), TestContext.CancellationToken);

            await repository.ClearAsync(TestContext.CancellationToken);

            var history = await repository.ListRecentAsync(cancellationToken: TestContext.CancellationToken);

            Assert.IsEmpty(history);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    private static DapperQueryHistoryRepository CreateRepository(out string dbPath)
    {
        dbPath = Path.Combine(
            Path.GetTempPath(),
            $"hunting-query-history-{Guid.NewGuid():N}.db");

        var connectionFactory = new SqliteAppDbConnectionFactory(BuildConnectionString(dbPath));
        return new DapperQueryHistoryRepository(connectionFactory);
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
