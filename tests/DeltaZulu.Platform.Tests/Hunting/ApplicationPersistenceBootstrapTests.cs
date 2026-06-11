namespace DeltaZulu.Platform.Tests.Hunting;

using DeltaZulu.Platform.Application.Hunting.QueryHistory;
using DeltaZulu.Platform.Application.Hunting.SavedQueries;
using DeltaZulu.Platform.Application.Hunting.Settings;
using DeltaZulu.Platform.Application.Hunting.Visualizations;
using DeltaZulu.Platform.Domain.Hunting.Samples;
using DeltaZulu.Platform.Data.Hunting.Persistence;
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
            var visualizationRepository = provider.GetRequiredService<IVisualizationRepository>();

            var settings = await settingsRepository.LoadAsync(TestContext.CancellationToken);
            Assert.AreEqual(UserSettingsDefaults.DefaultTimeFilterKey, settings.DefaultTimeFilter);
            Assert.IsNull(settings.DefaultResultLimit);

            var savedQueries = await savedQueryRepository.ListAsync(TestContext.CancellationToken);
            Assert.HasCount(SampleQueryCatalog.All.Count, savedQueries);
            Assert.IsTrue(savedQueries.All(static query => query.Id.StartsWith("sample-", StringComparison.Ordinal)));
            Assert.HasCount(24, savedQueries);

            var history = await queryHistoryRepository.ListRecentAsync(cancellationToken: TestContext.CancellationToken);
            Assert.IsEmpty(history);

            var visualizations = await visualizationRepository.ListAsync(TestContext.CancellationToken);
            Assert.IsEmpty(visualizations);
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
            var visualizationRepository = provider.GetRequiredService<IVisualizationRepository>();

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

            await visualizationRepository.SaveAsync(new VisualizationRecord(
                "visualization-1",
                "saved-1",
                "Saved chart",
                "Shared visualization persistence test.",
                "barchart",
                "{\"xcolumn\":\"DeviceName\",\"ycolumns\":[\"LaunchCount\"]}",
                now,
                now), TestContext.CancellationToken);

            var secondServices = new ServiceCollection();
            secondServices.AddApplicationPersistence(BuildConnectionString(dbPath));

            await using var secondProvider = secondServices.BuildServiceProvider();

            await secondProvider.InitializeApplicationPersistenceAsync(TestContext.CancellationToken);

            var loadedSettings = await secondProvider.GetRequiredService<IUserSettingsRepository>().LoadAsync(TestContext.CancellationToken);
            var loadedSavedQueries = await secondProvider.GetRequiredService<ISavedQueryRepository>().ListAsync(TestContext.CancellationToken);
            var loadedHistory = await secondProvider.GetRequiredService<IQueryHistoryRepository>().ListRecentAsync(cancellationToken: TestContext.CancellationToken);
            var loadedVisualizations = await secondProvider.GetRequiredService<IVisualizationRepository>().ListAsync(TestContext.CancellationToken);

            Assert.AreEqual("last24h", loadedSettings.DefaultTimeFilter);
            Assert.AreEqual(500, loadedSettings.DefaultResultLimit);
            Assert.HasCount(25, loadedSavedQueries);
            Assert.AreEqual("saved-1", loadedSavedQueries[0].Id);
            Assert.HasCount(1, loadedHistory);
            Assert.AreEqual("history-1", loadedHistory[0].Id);
            Assert.HasCount(1, loadedVisualizations);
            Assert.AreEqual("visualization-1", loadedVisualizations[0].Id);
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