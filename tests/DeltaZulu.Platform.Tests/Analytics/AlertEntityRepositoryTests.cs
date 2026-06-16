using DeltaZulu.Platform.Data.Sqlite.Analytics;
using DeltaZulu.Platform.Data.Sqlite.Analytics.AlertEntities;
using DeltaZulu.Platform.Domain.Analytics.AlertEntities;
using Microsoft.Data.Sqlite;

namespace DeltaZulu.Platform.Tests.Analytics;

[TestClass]
public sealed class AlertEntityRepositoryTests
{
    [TestMethod]
    public async Task ListByAlertAsync_ReturnsEmptyList_WhenStoreIsEmpty()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var entities = await repository.ListByAlertAsync("alert-001", TestContext.CancellationToken);

            Assert.IsEmpty(entities);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveBatchAsync_PersistsEntitiesAcrossRepositoryInstances()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);

            var entities = new[]
            {
                CreateEntity(id: "ent-001", alertId: "alert-001", entityType: "User", entityValue: "admin@contoso.com", createdAtUtc: now),
                CreateEntity(id: "ent-002", alertId: "alert-001", entityType: "Host", entityValue: "DC01.contoso.com", createdAtUtc: now),
                CreateEntity(id: "ent-003", alertId: "alert-001", entityType: "IP", entityValue: "10.0.0.5", createdAtUtc: now)
            };

            await repository.SaveBatchAsync(entities, TestContext.CancellationToken);

            var secondRepository = new DapperAlertEntityRepository(
                new SqliteAppDbConnectionFactory(BuildConnectionString(dbPath)));

            var saved = await secondRepository.ListByAlertAsync("alert-001", TestContext.CancellationToken);

            Assert.HasCount(3, saved);
            Assert.AreEqual("User", saved[0].EntityType);
            Assert.AreEqual("admin@contoso.com", saved[0].EntityValue);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task ListByAlertAsync_OrdersBySpecificityThenCriticality()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);

            var entities = new[]
            {
                CreateEntity(id: "ent-001", alertId: "alert-001", entityType: "IP", entityValue: "10.0.0.0/8",
                    specificityWeight: 0.2, criticalityWeight: 0.5, createdAtUtc: now),
                CreateEntity(id: "ent-002", alertId: "alert-001", entityType: "User", entityValue: "admin@contoso.com",
                    specificityWeight: 0.9, criticalityWeight: 0.8, createdAtUtc: now),
                CreateEntity(id: "ent-003", alertId: "alert-001", entityType: "Host", entityValue: "DC01.contoso.com",
                    specificityWeight: 0.9, criticalityWeight: 0.95, createdAtUtc: now)
            };

            await repository.SaveBatchAsync(entities, TestContext.CancellationToken);

            var saved = await repository.ListByAlertAsync("alert-001", TestContext.CancellationToken);

            Assert.HasCount(3, saved);
            Assert.AreEqual("ent-003", saved[0].Id);
            Assert.AreEqual("ent-002", saved[1].Id);
            Assert.AreEqual("ent-001", saved[2].Id);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task ListByEntityValueAsync_FindsEntitiesAcrossAlerts()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);

            var entities = new[]
            {
                CreateEntity(id: "ent-001", alertId: "alert-001", entityType: "User", entityValue: "admin@contoso.com", createdAtUtc: now),
                CreateEntity(id: "ent-002", alertId: "alert-002", entityType: "User", entityValue: "admin@contoso.com", createdAtUtc: now.AddMinutes(5)),
                CreateEntity(id: "ent-003", alertId: "alert-003", entityType: "User", entityValue: "other@contoso.com", createdAtUtc: now)
            };

            await repository.SaveBatchAsync(entities, TestContext.CancellationToken);

            var saved = await repository.ListByEntityValueAsync("User", "admin@contoso.com", TestContext.CancellationToken);

            Assert.HasCount(2, saved);
            Assert.AreEqual("alert-002", saved[0].AlertId);
            Assert.AreEqual("alert-001", saved[1].AlertId);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveBatchAsync_IgnoresDuplicateIds()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);

            var batch1 = new[] { CreateEntity(id: "ent-001", alertId: "alert-001", entityType: "User", entityValue: "admin@contoso.com", createdAtUtc: now) };
            var batch2 = new[] { CreateEntity(id: "ent-001", alertId: "alert-001", entityType: "User", entityValue: "different@contoso.com", createdAtUtc: now) };

            await repository.SaveBatchAsync(batch1, TestContext.CancellationToken);
            await repository.SaveBatchAsync(batch2, TestContext.CancellationToken);

            var saved = await repository.ListByAlertAsync("alert-001", TestContext.CancellationToken);

            Assert.HasCount(1, saved);
            Assert.AreEqual("admin@contoso.com", saved[0].EntityValue);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task HighFanoutFlag_PersistsCorrectly()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);

            var entities = new[]
            {
                CreateEntity(id: "ent-001", alertId: "alert-001", entityType: "IP", entityValue: "10.0.0.0/8",
                    isHighFanout: true, createdAtUtc: now),
                CreateEntity(id: "ent-002", alertId: "alert-001", entityType: "User", entityValue: "admin@contoso.com",
                    isHighFanout: false, createdAtUtc: now)
            };

            await repository.SaveBatchAsync(entities, TestContext.CancellationToken);

            var saved = await repository.ListByAlertAsync("alert-001", TestContext.CancellationToken);

            var highFanout = saved.First(e => e.Id == "ent-001");
            var normal = saved.First(e => e.Id == "ent-002");

            Assert.IsTrue(highFanout.IsHighFanout);
            Assert.IsFalse(normal.IsHighFanout);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    private static AlertEntityRecord CreateEntity(
        string id = "ent-001",
        string alertId = "alert-001",
        string entityType = "User",
        string entityValue = "admin@contoso.com",
        string role = "Source",
        double specificityWeight = 0.8,
        double criticalityWeight = 0.7,
        bool isHighFanout = false,
        DateTime createdAtUtc = default) => new AlertEntityRecord(
            Id: id,
            AlertId: alertId,
            EntityType: entityType,
            EntityValue: entityValue,
            Role: role,
            SpecificityWeight: specificityWeight,
            CriticalityWeight: criticalityWeight,
            IsHighFanout: isHighFanout,
            CreatedAtUtc: createdAtUtc);

    private static DapperAlertEntityRepository CreateRepository(out string dbPath)
    {
        dbPath = Path.Combine(
            Path.GetTempPath(),
            $"alert-entities-{Guid.NewGuid():N}.db");

        var connectionFactory = new SqliteAppDbConnectionFactory(BuildConnectionString(dbPath));
        return new DapperAlertEntityRepository(connectionFactory);
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