
using DeltaZulu.Platform.Data.Sqlite.Analytics;
using DeltaZulu.Platform.Data.Sqlite.Analytics.Detections;
using DeltaZulu.Platform.Domain.Analytics.Detections;
using Microsoft.Data.Sqlite;

namespace DeltaZulu.Platform.Tests.Analytics;
[TestClass]
public sealed class DetectionRepositoryTests
{
    [TestMethod]
    public async Task ListAsync_ReturnsEmptyList_WhenStoreIsEmpty()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var detections = await repository.ListAsync(TestContext.CancellationToken);

            Assert.IsEmpty(detections);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_PersistsDetectionAcrossRepositoryInstances()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc);

            await repository.SaveAsync(CreateDetection(
                id: "det-v1",
                detectionId: "det-001",
                version: 1,
                name: "Suspicious PowerShell",
                queryText: "ProcessEvent | where FileName has 'powershell' | where CommandLine has '-enc'",
                createdAtUtc: now,
                updatedAtUtc: now), TestContext.CancellationToken);

            var secondRepository = new DapperDetectionRepository(
                new SqliteAppDbConnectionFactory(BuildConnectionString(dbPath)));

            var saved = await secondRepository.GetAsync("det-v1", TestContext.CancellationToken);

            Assert.IsNotNull(saved);
            Assert.AreEqual("det-001", saved.DetectionId);
            Assert.AreEqual(1, saved.Version);
            Assert.AreEqual("Suspicious PowerShell", saved.Name);
            Assert.AreEqual("High", saved.Severity);
            Assert.AreEqual("Medium", saved.Confidence);
            Assert.AreEqual(75, saved.RiskScore);
            Assert.IsTrue(saved.IsEnabled);
            Assert.AreEqual(now, saved.CreatedAtUtc);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task ListAsync_ReturnsOnlyLatestVersionPerDetection()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc);
            var later = new DateTime(2026, 6, 8, 11, 0, 0, DateTimeKind.Utc);

            await repository.SaveAsync(CreateDetection(
                id: "det-001-v1",
                detectionId: "det-001",
                version: 1,
                name: "Rule v1",
                createdAtUtc: now,
                updatedAtUtc: now), TestContext.CancellationToken);

            await repository.SaveAsync(CreateDetection(
                id: "det-001-v2",
                detectionId: "det-001",
                version: 2,
                name: "Rule v2",
                createdAtUtc: later,
                updatedAtUtc: later), TestContext.CancellationToken);

            var list = await repository.ListAsync(TestContext.CancellationToken);

            Assert.HasCount(1, list);
            Assert.AreEqual("det-001-v2", list[0].Id);
            Assert.AreEqual(2, list[0].Version);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task ListVersionsAsync_ReturnsAllVersionsDescending()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc);

            await repository.SaveAsync(CreateDetection(
                id: "det-001-v1", detectionId: "det-001", version: 1,
                createdAtUtc: now, updatedAtUtc: now), TestContext.CancellationToken);

            await repository.SaveAsync(CreateDetection(
                id: "det-001-v2", detectionId: "det-001", version: 2,
                createdAtUtc: now, updatedAtUtc: now), TestContext.CancellationToken);

            await repository.SaveAsync(CreateDetection(
                id: "det-001-v3", detectionId: "det-001", version: 3,
                createdAtUtc: now, updatedAtUtc: now), TestContext.CancellationToken);

            var versions = await repository.ListVersionsAsync("det-001", TestContext.CancellationToken);

            Assert.HasCount(3, versions);
            Assert.AreEqual(3, versions[0].Version);
            Assert.AreEqual(2, versions[1].Version);
            Assert.AreEqual(1, versions[2].Version);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task GetLatestVersionAsync_ReturnsHighestVersion()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc);

            await repository.SaveAsync(CreateDetection(
                id: "det-001-v1", detectionId: "det-001", version: 1,
                createdAtUtc: now, updatedAtUtc: now), TestContext.CancellationToken);

            await repository.SaveAsync(CreateDetection(
                id: "det-001-v2", detectionId: "det-001", version: 2,
                name: "Latest",
                createdAtUtc: now, updatedAtUtc: now), TestContext.CancellationToken);

            var latest = await repository.GetLatestVersionAsync("det-001", TestContext.CancellationToken);

            Assert.IsNotNull(latest);
            Assert.AreEqual("det-001-v2", latest.Id);
            Assert.AreEqual("Latest", latest.Name);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SetEnabledAsync_DisablesAllVersions()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc);

            await repository.SaveAsync(CreateDetection(
                id: "det-001-v1", detectionId: "det-001", version: 1,
                createdAtUtc: now, updatedAtUtc: now), TestContext.CancellationToken);

            await repository.SetEnabledAsync("det-001", false, TestContext.CancellationToken);

            var saved = await repository.GetAsync("det-001-v1", TestContext.CancellationToken);

            Assert.IsNotNull(saved);
            Assert.IsFalse(saved.IsEnabled);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task DeleteAsync_RemovesDetectionVersion()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc);

            await repository.SaveAsync(CreateDetection(
                id: "det-001-v1", detectionId: "det-001", version: 1,
                createdAtUtc: now, updatedAtUtc: now), TestContext.CancellationToken);

            await repository.DeleteAsync("det-001-v1", TestContext.CancellationToken);

            var saved = await repository.GetAsync("det-001-v1", TestContext.CancellationToken);

            Assert.IsNull(saved);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    private static DetectionRecord CreateDetection(
        string id = "det-v1",
        string detectionId = "det-001",
        int version = 1,
        string name = "Test Detection",
        string? queryText = null,
        DateTime createdAtUtc = default,
        DateTime updatedAtUtc = default) => new DetectionRecord(
            Id: id,
            DetectionId: detectionId,
            Version: version,
            RuleHash: "sha256-test-hash",
            Name: name,
            Description: "Test detection description",
            QueryText: queryText ?? "ProcessEvent | where FileName has 'cmd.exe'",
            Severity: "High",
            Confidence: "Medium",
            RiskScore: 75,
            MitreTactics: "[\"Execution\"]",
            MitreTechniques: "[\"T1059.001\"]",
            EntityMappingHints: null,
            ScheduleCron: "0 */5 * * *",
            SuppressionPolicyJson: null,
            IsEnabled: true,
            TestMetadataJson: null,
            CreatedAtUtc: createdAtUtc,
            UpdatedAtUtc: updatedAtUtc);

    private static DapperDetectionRepository CreateRepository(out string dbPath)
    {
        dbPath = Path.Combine(
            Path.GetTempPath(),
            $"hunting-detections-{Guid.NewGuid():N}.db");

        var connectionFactory = new SqliteAppDbConnectionFactory(BuildConnectionString(dbPath));
        return new DapperDetectionRepository(connectionFactory);
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