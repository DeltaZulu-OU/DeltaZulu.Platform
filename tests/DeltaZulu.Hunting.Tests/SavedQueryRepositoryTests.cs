namespace DeltaZulu.Hunting.Tests;

using DeltaZulu.Hunting.Data.Persistence;
using DeltaZulu.Hunting.Data.SavedQueries;
using Microsoft.Data.Sqlite;
using AppSavedQueryRecord = Hunting.Application.SavedQueries.SavedQueryRecord;

[TestClass]
public sealed class SavedQueryRepositoryTests
{
    [TestMethod]
    public async Task ListAsync_ReturnsEmptyList_WhenStoreIsEmpty()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var savedQueries = await repository.ListAsync(TestContext.CancellationToken);

            Assert.IsEmpty(savedQueries);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_PersistsQueryAcrossRepositoryInstances()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var createdAt = new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc);
            var updatedAt = new DateTime(2026, 6, 3, 8, 5, 0, DateTimeKind.Utc);

            await repository.SaveAsync(new AppSavedQueryRecord(
                "query-1",
                "Recent PowerShell",
                "Find recent PowerShell launches.",
                "ProcessEvent | where FileName has \"powershell\"",
                createdAt,
                updatedAt,
                null), TestContext.CancellationToken);

            var secondRepository = new DapperSavedQueryRepository(
                new SqliteAppDbConnectionFactory(BuildConnectionString(dbPath)));

            var saved = await secondRepository.GetAsync("query-1", TestContext.CancellationToken);

            Assert.IsNotNull(saved);
            Assert.AreEqual("Recent PowerShell", saved.Name);
            Assert.AreEqual("Find recent PowerShell launches.", saved.Description);
            Assert.AreEqual("ProcessEvent | where FileName has \"powershell\"", saved.QueryText);
            Assert.AreEqual(createdAt, saved.CreatedAt);
            Assert.AreEqual(updatedAt, saved.UpdatedAt);
            Assert.IsNull(saved.LastRunAt);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_UpdatesExistingQuery()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var createdAt = new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc);
            var firstUpdatedAt = new DateTime(2026, 6, 3, 8, 5, 0, DateTimeKind.Utc);
            var secondUpdatedAt = new DateTime(2026, 6, 3, 8, 10, 0, DateTimeKind.Utc);

            await repository.SaveAsync(new AppSavedQueryRecord(
                "query-1",
                "Original",
                null,
                "ProcessEvent | take 10",
                createdAt,
                firstUpdatedAt,
                null), TestContext.CancellationToken);

            await repository.SaveAsync(new AppSavedQueryRecord(
                "query-1",
                "Updated",
                "Updated description",
                "ProcessEvent | take 25",
                createdAt,
                secondUpdatedAt,
                null), TestContext.CancellationToken);

            var saved = await repository.GetAsync("query-1", TestContext.CancellationToken);

            Assert.IsNotNull(saved);
            Assert.AreEqual("Updated", saved.Name);
            Assert.AreEqual("Updated description", saved.Description);
            Assert.AreEqual("ProcessEvent | take 25", saved.QueryText);
            Assert.AreEqual(createdAt, saved.CreatedAt);
            Assert.AreEqual(secondUpdatedAt, saved.UpdatedAt);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task MarkRunAsync_UpdatesLastRunAndUpdatedAt()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var createdAt = new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc);
            var updatedAt = new DateTime(2026, 6, 3, 8, 5, 0, DateTimeKind.Utc);
            var runAt = new DateTime(2026, 6, 3, 9, 0, 0, DateTimeKind.Utc);

            await repository.SaveAsync(new AppSavedQueryRecord(
                "query-1",
                "Query",
                null,
                "ProcessEvent | take 10",
                createdAt,
                updatedAt,
                null), TestContext.CancellationToken);

            await repository.MarkRunAsync("query-1", runAt, TestContext.CancellationToken);

            var saved = await repository.GetAsync("query-1", TestContext.CancellationToken);

            Assert.IsNotNull(saved);
            Assert.AreEqual(runAt, saved.LastRunAt);
            Assert.AreEqual(runAt, saved.UpdatedAt);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task DeleteAsync_RemovesSavedQuery()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc);

            await repository.SaveAsync(new AppSavedQueryRecord(
                "query-1",
                "Query",
                null,
                "ProcessEvent | take 10",
                now,
                now,
                null), TestContext.CancellationToken);

            await repository.DeleteAsync("query-1", TestContext.CancellationToken);

            var saved = await repository.GetAsync("query-1", TestContext.CancellationToken);

            Assert.IsNull(saved);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    private static DapperSavedQueryRepository CreateRepository(out string dbPath)
    {
        dbPath = Path.Combine(
            Path.GetTempPath(),
            $"hunting-saved-queries-{Guid.NewGuid():N}.db");

        var connectionFactory = new SqliteAppDbConnectionFactory(BuildConnectionString(dbPath));
        return new DapperSavedQueryRepository(connectionFactory);
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