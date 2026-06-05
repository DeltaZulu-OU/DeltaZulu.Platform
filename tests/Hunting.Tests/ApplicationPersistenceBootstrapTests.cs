namespace Hunting.Tests;

using Hunting.Data.Persistence;
using Hunting.Data.QueryHistory;
using Hunting.Data.SavedQueries;
using Hunting.Data.Settings;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

[TestClass]
public sealed class ApplicationPersistenceBootstrapTests
{
    [TestMethod]
    public async Task InitializeApplicationPersistenceAsync_RegistersAndInitializesAllRepositories()
    {
        var dbPath = Path.Combine(
            Path.GetTempPath(),
            $"hunting-app-persistence-{Guid.NewGuid():N}.db");

        try
        {
            var services = new ServiceCollection();
            services.AddApplicationPersistence(BuildConnectionString(dbPath));

            await using var provider = services.BuildServiceProvider();

            await provider.InitializeApplicationPersistenceAsync(TestContext.CancellationToken);

            var settingsRepository = provider.GetRequiredService<IUserSettingsRepository>();
            var savedQueryRepository = provider.GetRequiredService<ISavedQueryRepository>();
            var queryHistoryRepository = provider.GetRequiredService<IQueryHistoryRepository>();

            var settings = await settingsRepository.LoadAsync(TestContext.CancellationToken);
            Assert.AreEqual(UserSettingsDefaults.DefaultTimeFilterKey, settings.DefaultTimeFilter);
            Assert.IsNull(settings.DefaultResultLimit);

            var savedQueries = await savedQueryRepository.ListAsync(TestContext.CancellationToken);
            Assert.IsEmpty(savedQueries);

            var history = await queryHistoryRepository.ListRecentAsync(cancellationToken: TestContext.CancellationToken);
            Assert.IsEmpty(history);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task ApplicationPersistenceRepositories_ShareTheSameSqliteDatabase()
    {
        var dbPath = Path.Combine(
            Path.GetTempPath(),
            $"hunting-app-persistence-shared-{Guid.NewGuid():N}.db");

        try
        {
            var services = new ServiceCollection();
            services.AddApplicationPersistence(BuildConnectionString(dbPath));

            await using var provider = services.BuildServiceProvider();

            await provider.InitializeApplicationPersistenceAsync(TestContext.CancellationToken);

            var settingsRepository = provider.GetRequiredService<IUserSettingsRepository>();
            var savedQueryRepository = provider.GetRequiredService<ISavedQueryRepository>();
            var queryHistoryRepository = provider.GetRequiredService<IQueryHistoryRepository>();

            await settingsRepository.SaveAsync(new UserSettingsRecord("last24h", 500), TestContext.CancellationToken);

            var now = new DateTime(2026, 6, 3, 12, 0, 0, DateTimeKind.Utc);
            await savedQueryRepository.SaveAsync(new SavedQueryRecord(
                "saved-1",
                "Saved query",
                "Shared persistence test.",
                "ProcessEvent | take 10",
                now,
                now,
                null), TestContext.CancellationToken);

            await queryHistoryRepository.AddAsync(new QueryHistoryRecord(
                "history-1",
                "ProcessEvent | take 10",
                now,
                true,
                10,
                15,
                null), TestContext.CancellationToken);

            var secondServices = new ServiceCollection();
            secondServices.AddApplicationPersistence(BuildConnectionString(dbPath));

            await using var secondProvider = secondServices.BuildServiceProvider();

            await secondProvider.InitializeApplicationPersistenceAsync(TestContext.CancellationToken);

            var loadedSettings = await secondProvider.GetRequiredService<IUserSettingsRepository>().LoadAsync(TestContext.CancellationToken);
            var loadedSavedQueries = await secondProvider.GetRequiredService<ISavedQueryRepository>().ListAsync(TestContext.CancellationToken);
            var loadedHistory = await secondProvider.GetRequiredService<IQueryHistoryRepository>().ListRecentAsync(cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual("last24h", loadedSettings.DefaultTimeFilter);
            Assert.AreEqual(500, loadedSettings.DefaultResultLimit);
            Assert.HasCount(1, loadedSavedQueries);
            Assert.AreEqual("saved-1", loadedSavedQueries[0].Id);
            Assert.HasCount(1, loadedHistory);
            Assert.AreEqual("history-1", loadedHistory[0].Id);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
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