
using DeltaZulu.Platform.Data.Sqlite.Analytics;
using DeltaZulu.Platform.Data.Sqlite.Analytics.DetectionRuns;
using DeltaZulu.Platform.Domain.Analytics.DetectionRuns;
using Microsoft.Data.Sqlite;

namespace DeltaZulu.Platform.Tests.Analytics;
[TestClass]
public sealed class DetectionRunRepositoryTests
{
    [TestMethod]
    public async Task ListByDetectionAsync_ReturnsEmptyList_WhenStoreIsEmpty()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var runs = await repository.ListByDetectionAsync("det-001", TestContext.CancellationToken);

            Assert.IsEmpty(runs);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_PersistsRunAcrossRepositoryInstances()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var windowStart = new DateTime(2026, 6, 8, 9, 0, 0, DateTimeKind.Utc);
            var windowEnd = new DateTime(2026, 6, 8, 9, 5, 0, DateTimeKind.Utc);
            var startedAt = new DateTime(2026, 6, 8, 9, 5, 1, DateTimeKind.Utc);
            var completedAt = new DateTime(2026, 6, 8, 9, 5, 3, DateTimeKind.Utc);

            await repository.SaveAsync(new DetectionRunRecord(
                Id: "run-001",
                DetectionId: "det-001",
                DetectionVersion: 2,
                RuleHash: "sha256-rule-hash",
                ExecutionWindowStartUtc: windowStart,
                ExecutionWindowEndUtc: windowEnd,
                Status: "Completed",
                ResultCount: 5,
                DurationMs: 2300,
                ErrorMessage: null,
                QueryHash: "sha256-query-hash",
                StartedAtUtc: startedAt,
                CompletedAtUtc: completedAt), TestContext.CancellationToken);

            var secondRepository = new DapperDetectionRunRepository(
                new SqliteAppDbConnectionFactory(BuildConnectionString(dbPath)));

            var saved = await secondRepository.GetAsync("run-001", TestContext.CancellationToken);

            Assert.IsNotNull(saved);
            Assert.AreEqual("det-001", saved.DetectionId);
            Assert.AreEqual(2, saved.DetectionVersion);
            Assert.AreEqual("Completed", saved.Status);
            Assert.AreEqual(5, saved.ResultCount);
            Assert.AreEqual(2300L, saved.DurationMs);
            Assert.IsNull(saved.ErrorMessage);
            Assert.AreEqual(windowStart, saved.ExecutionWindowStartUtc);
            Assert.AreEqual(windowEnd, saved.ExecutionWindowEndUtc);
            Assert.AreEqual(startedAt, saved.StartedAtUtc);
            Assert.AreEqual(completedAt, saved.CompletedAtUtc);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_UpdatesStatusOnConflict()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var windowStart = new DateTime(2026, 6, 8, 9, 0, 0, DateTimeKind.Utc);
            var windowEnd = new DateTime(2026, 6, 8, 9, 5, 0, DateTimeKind.Utc);
            var startedAt = new DateTime(2026, 6, 8, 9, 5, 1, DateTimeKind.Utc);
            var completedAt = new DateTime(2026, 6, 8, 9, 5, 4, DateTimeKind.Utc);

            await repository.SaveAsync(new DetectionRunRecord(
                "run-001", "det-001", 1, "hash", windowStart, windowEnd,
                "Running", 0, 0, null, "qhash", startedAt, null),
                TestContext.CancellationToken);

            await repository.SaveAsync(new DetectionRunRecord(
                "run-001", "det-001", 1, "hash", windowStart, windowEnd,
                "Completed", 3, 3200, null, "qhash", startedAt, completedAt),
                TestContext.CancellationToken);

            var saved = await repository.GetAsync("run-001", TestContext.CancellationToken);

            Assert.IsNotNull(saved);
            Assert.AreEqual("Completed", saved.Status);
            Assert.AreEqual(3, saved.ResultCount);
            Assert.AreEqual(3200L, saved.DurationMs);
            Assert.AreEqual(completedAt, saved.CompletedAtUtc);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_PersistsFailedRunWithError()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var windowStart = new DateTime(2026, 6, 8, 9, 0, 0, DateTimeKind.Utc);
            var windowEnd = new DateTime(2026, 6, 8, 9, 5, 0, DateTimeKind.Utc);
            var startedAt = new DateTime(2026, 6, 8, 9, 5, 1, DateTimeKind.Utc);
            var completedAt = new DateTime(2026, 6, 8, 9, 5, 2, DateTimeKind.Utc);

            await repository.SaveAsync(new DetectionRunRecord(
                "run-002", "det-001", 1, "hash", windowStart, windowEnd,
                "Failed", 0, 800, "KQL parse error: unexpected token", "qhash",
                startedAt, completedAt), TestContext.CancellationToken);

            var saved = await repository.GetAsync("run-002", TestContext.CancellationToken);

            Assert.IsNotNull(saved);
            Assert.AreEqual("Failed", saved.Status);
            Assert.AreEqual(0, saved.ResultCount);
            Assert.AreEqual("KQL parse error: unexpected token", saved.ErrorMessage);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task ListByDetectionAsync_ReturnsRunsDescendingByStartTime()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var baseTime = new DateTime(2026, 6, 8, 9, 0, 0, DateTimeKind.Utc);

            for (var i = 1; i <= 3; i++)
            {
                var startedAt = baseTime.AddMinutes(i * 5);
                await repository.SaveAsync(new DetectionRunRecord(
                    $"run-{i:D3}", "det-001", 1, "hash",
                    startedAt.AddMinutes(-5), startedAt,
                    "Completed", i, 1000, null, "qhash",
                    startedAt, startedAt.AddSeconds(2)),
                    TestContext.CancellationToken);
            }

            var runs = await repository.ListByDetectionAsync("det-001", TestContext.CancellationToken);

            Assert.HasCount(3, runs);
            Assert.AreEqual("run-003", runs[0].Id);
            Assert.AreEqual("run-002", runs[1].Id);
            Assert.AreEqual("run-001", runs[2].Id);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    private static DapperDetectionRunRepository CreateRepository(out string dbPath)
    {
        dbPath = Path.Combine(
            Path.GetTempPath(),
            $"hunting-detection-runs-{Guid.NewGuid():N}.db");

        var connectionFactory = new SqliteAppDbConnectionFactory(BuildConnectionString(dbPath));
        return new DapperDetectionRunRepository(connectionFactory);
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