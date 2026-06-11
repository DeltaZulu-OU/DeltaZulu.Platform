namespace DeltaZulu.Platform.Tests.Hunting;

using DeltaZulu.Platform.Application.Hunting.Settings;
using DeltaZulu.Platform.Data.Hunting.Persistence;
using DeltaZulu.Platform.Data.Hunting.Settings;
using Microsoft.Data.Sqlite;

[TestClass]
public sealed class UserSettingsRepositoryTests
{
    [TestMethod]
    public async Task LoadAsync_ReturnsDefaultSettings_WhenStoreIsEmpty()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var settings = await repository.LoadAsync(TestContext.CancellationToken);

            Assert.AreEqual(UserSettingsDefaults.DefaultTimeFilterKey, settings.DefaultTimeFilter);
            Assert.IsNull(settings.DefaultResultLimit);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_PersistsSettingsAcrossRepositoryInstances()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            await repository.SaveAsync(new UserSettingsRecord("last24h", 500), TestContext.CancellationToken);

            var secondRepository = new DapperUserSettingsRepository(
                new SqliteAppDbConnectionFactory(BuildConnectionString(dbPath)));

            var settings = await secondRepository.LoadAsync(TestContext.CancellationToken);

            Assert.AreEqual("last24h", settings.DefaultTimeFilter);
            Assert.AreEqual(500, settings.DefaultResultLimit);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    private static DapperUserSettingsRepository CreateRepository(out string dbPath)
    {
        dbPath = Path.Combine(
            Path.GetTempPath(),
            $"hunting-settings-{Guid.NewGuid():N}.db");

        var connectionFactory = new SqliteAppDbConnectionFactory(BuildConnectionString(dbPath));
        return new DapperUserSettingsRepository(connectionFactory);
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