using DeltaZulu.Platform.Data.Sqlite.Analytics;
using DeltaZulu.Platform.Data.Sqlite.Analytics.Alerts;
using DeltaZulu.Platform.Domain.Analytics.Alerts;
using Microsoft.Data.Sqlite;

namespace DeltaZulu.Platform.Tests.Analytics;

[TestClass]
public sealed class AlertRepositoryTests
{
    [TestMethod]
    public async Task ListByRunAsync_ReturnsEmptyList_WhenStoreIsEmpty()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var alerts = await repository.ListByRunAsync("run-001", TestContext.CancellationToken);

            Assert.IsEmpty(alerts);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_PersistsAlertAcrossRepositoryInstances()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var alertTime = new DateTime(2026, 6, 8, 9, 3, 0, DateTimeKind.Utc);
            var now = new DateTime(2026, 6, 8, 9, 5, 0, DateTimeKind.Utc);

            await repository.SaveAsync(CreateAlert(
                id: "alert-001",
                alertTimeUtc: alertTime,
                createdAtUtc: now), TestContext.CancellationToken);

            var secondRepository = new DapperAlertRepository(
                new SqliteAppDbConnectionFactory(BuildConnectionString(dbPath)));

            var saved = await secondRepository.GetAsync("alert-001", TestContext.CancellationToken);

            Assert.IsNotNull(saved);
            Assert.AreEqual("det-001", saved.DetectionId);
            Assert.AreEqual(2, saved.DetectionVersion);
            Assert.AreEqual("run-001", saved.DetectionRunId);
            Assert.AreEqual(alertTime, saved.AlertTimeUtc);
            Assert.AreEqual("ProcessEvent", saved.SourceView);
            Assert.AreEqual("High", saved.Severity);
            Assert.AreEqual("Medium", saved.Confidence);
            Assert.AreEqual(75, saved.RiskScore);
            Assert.AreEqual(now, saved.CreatedAtUtc);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveBatchAsync_PersistsMultipleAlerts()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 8, 9, 5, 0, DateTimeKind.Utc);

            var alerts = new[]
            {
                CreateAlert(id: "alert-001", alertTimeUtc: now.AddMinutes(-2), createdAtUtc: now),
                CreateAlert(id: "alert-002", alertTimeUtc: now.AddMinutes(-1), createdAtUtc: now),
                CreateAlert(id: "alert-003", alertTimeUtc: now, createdAtUtc: now)
            };

            await repository.SaveBatchAsync(alerts, TestContext.CancellationToken);

            var saved = await repository.ListByRunAsync("run-001", TestContext.CancellationToken);

            Assert.HasCount(3, saved);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task ListByDetectionAsync_ReturnsAlertsDescendingByAlertTime()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var baseTime = new DateTime(2026, 6, 8, 9, 0, 0, DateTimeKind.Utc);
            var now = new DateTime(2026, 6, 8, 9, 5, 0, DateTimeKind.Utc);

            await repository.SaveAsync(CreateAlert(
                id: "alert-001", alertTimeUtc: baseTime.AddMinutes(1),
                createdAtUtc: now), TestContext.CancellationToken);

            await repository.SaveAsync(CreateAlert(
                id: "alert-002", alertTimeUtc: baseTime.AddMinutes(3),
                createdAtUtc: now), TestContext.CancellationToken);

            await repository.SaveAsync(CreateAlert(
                id: "alert-003", alertTimeUtc: baseTime.AddMinutes(2),
                createdAtUtc: now), TestContext.CancellationToken);

            var alerts = await repository.ListByDetectionAsync("det-001", TestContext.CancellationToken);

            Assert.HasCount(3, alerts);
            Assert.AreEqual("alert-002", alerts[0].Id);
            Assert.AreEqual("alert-003", alerts[1].Id);
            Assert.AreEqual("alert-001", alerts[2].Id);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_DuplicateIdIsIgnored()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var alertTime = new DateTime(2026, 6, 8, 9, 3, 0, DateTimeKind.Utc);
            var now = new DateTime(2026, 6, 8, 9, 5, 0, DateTimeKind.Utc);

            await repository.SaveAsync(CreateAlert(
                id: "alert-001", alertTimeUtc: alertTime,
                createdAtUtc: now), TestContext.CancellationToken);

            // Second save with same id and different data — should be silently ignored
            await repository.SaveAsync(new AlertRecord(
                Id: "alert-001",
                DetectionId: "det-002",
                DetectionVersion: 99,
                DetectionRunId: "run-999",
                AlertTimeUtc: alertTime.AddHours(1),
                SourceView: "NetworkSession",
                SourceEventId: "different",
                Severity: "Low",
                Confidence: "Low",
                RiskScore: 10,
                EvidenceJson: "{}",
                CreatedAtUtc: now.AddMinutes(30)), TestContext.CancellationToken);

            var saved = await repository.GetAsync("alert-001", TestContext.CancellationToken);

            Assert.IsNotNull(saved);
            // Original data preserved because duplicate insert is ignored
            Assert.AreEqual("det-001", saved.DetectionId);
            Assert.AreEqual("run-001", saved.DetectionRunId);
            Assert.AreEqual(alertTime, saved.AlertTimeUtc);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    private static AlertRecord CreateAlert(
        string id = "alert-001",
        DateTime alertTimeUtc = default,
        DateTime createdAtUtc = default) => new AlertRecord(
            Id: id,
            DetectionId: "det-001",
            DetectionVersion: 2,
            DetectionRunId: "run-001",
            AlertTimeUtc: alertTimeUtc,
            SourceView: "ProcessEvent",
            SourceEventId: "evt-abc123",
            Severity: "High",
            Confidence: "Medium",
            RiskScore: 75,
            EvidenceJson: "{\"FileName\":\"powershell.exe\",\"CommandLine\":\"powershell -enc base64...\"}",
            CreatedAtUtc: createdAtUtc);

    private static DapperAlertRepository CreateRepository(out string dbPath)
    {
        dbPath = Path.Combine(
            Path.GetTempPath(),
            $"hunting-alerts-{Guid.NewGuid():N}.db");

        var connectionFactory = new SqliteAppDbConnectionFactory(BuildConnectionString(dbPath));
        return new DapperAlertRepository(connectionFactory);
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
