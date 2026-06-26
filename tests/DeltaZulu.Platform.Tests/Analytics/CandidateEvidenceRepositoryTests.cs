using DeltaZulu.Platform.Data.Sqlite.Analytics;
using DeltaZulu.Platform.Data.Sqlite.Analytics.Candidates;
using DeltaZulu.Platform.Domain.Analytics.Candidates;
using Microsoft.Data.Sqlite;

namespace DeltaZulu.Platform.Tests.Analytics;

[TestClass]
public sealed class CandidateEvidenceRepositoryTests
{
    [TestMethod]
    public async Task ListByCandidateAsync_ReturnsEmptyList_WhenStoreIsEmpty()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var evidence = await repository.ListByCandidateAsync("cand-001", TestContext.CancellationToken);

            Assert.IsEmpty(evidence);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_PersistsEvidenceAcrossRepositoryInstances()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);

            await repository.SaveAsync(CreateEvidence(
                id: "ev-001",
                candidateId: "cand-001",
                evidenceType: "Timeline",
                collectedAtUtc: now), TestContext.CancellationToken);

            var secondRepository = new DapperCandidateEvidenceRepository(
                new SqliteOperationsDbConnectionFactory(BuildConnectionString(dbPath)));

            var saved = await secondRepository.ListByCandidateAsync("cand-001", TestContext.CancellationToken);

            Assert.HasCount(1, saved);
            Assert.AreEqual("ev-001", saved[0].Id);
            Assert.AreEqual("Timeline", saved[0].EvidenceType);
            Assert.AreEqual(now, saved[0].CollectedAtUtc);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveBatchAsync_PersistsMultipleEvidenceItems()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);

            var evidence = new[]
            {
                CreateEvidence(id: "ev-001", candidateId: "cand-001", evidenceType: "Timeline", collectedAtUtc: now),
                CreateEvidence(id: "ev-002", candidateId: "cand-001", evidenceType: "RelatedLogs", collectedAtUtc: now.AddMinutes(1)),
                CreateEvidence(id: "ev-003", candidateId: "cand-001", evidenceType: "EntitySummary", collectedAtUtc: now.AddMinutes(2))
            };

            await repository.SaveBatchAsync(evidence, TestContext.CancellationToken);

            var saved = await repository.ListByCandidateAsync("cand-001", TestContext.CancellationToken);

            Assert.HasCount(3, saved);
            Assert.AreEqual("ev-001", saved[0].Id);
            Assert.AreEqual("ev-002", saved[1].Id);
            Assert.AreEqual("ev-003", saved[2].Id);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task ListByCandidateAsync_OrdersByCollectedTimeAscending()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var baseTime = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);

            var evidence = new[]
            {
                CreateEvidence(id: "ev-003", candidateId: "cand-001", collectedAtUtc: baseTime.AddMinutes(20)),
                CreateEvidence(id: "ev-001", candidateId: "cand-001", collectedAtUtc: baseTime),
                CreateEvidence(id: "ev-002", candidateId: "cand-001", collectedAtUtc: baseTime.AddMinutes(10))
            };

            await repository.SaveBatchAsync(evidence, TestContext.CancellationToken);

            var saved = await repository.ListByCandidateAsync("cand-001", TestContext.CancellationToken);

            Assert.HasCount(3, saved);
            Assert.AreEqual("ev-001", saved[0].Id);
            Assert.AreEqual("ev-002", saved[1].Id);
            Assert.AreEqual("ev-003", saved[2].Id);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task ListByCandidateAsync_DoesNotReturnOtherCandidateEvidence()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);

            var evidence = new[]
            {
                CreateEvidence(id: "ev-001", candidateId: "cand-001", collectedAtUtc: now),
                CreateEvidence(id: "ev-002", candidateId: "cand-002", collectedAtUtc: now),
                CreateEvidence(id: "ev-003", candidateId: "cand-001", collectedAtUtc: now.AddMinutes(1))
            };

            await repository.SaveBatchAsync(evidence, TestContext.CancellationToken);

            var cand1Evidence = await repository.ListByCandidateAsync("cand-001", TestContext.CancellationToken);
            var cand2Evidence = await repository.ListByCandidateAsync("cand-002", TestContext.CancellationToken);

            Assert.HasCount(2, cand1Evidence);
            Assert.HasCount(1, cand2Evidence);
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

            await repository.SaveAsync(CreateEvidence(
                id: "ev-001", candidateId: "cand-001", evidenceType: "Timeline",
                collectedAtUtc: now), TestContext.CancellationToken);

            await repository.SaveAsync(CreateEvidence(
                id: "ev-001", candidateId: "cand-001", evidenceType: "RelatedLogs",
                collectedAtUtc: now), TestContext.CancellationToken);

            var saved = await repository.ListByCandidateAsync("cand-001", TestContext.CancellationToken);

            Assert.HasCount(1, saved);
            Assert.AreEqual("Timeline", saved[0].EvidenceType);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    private static CandidateEvidenceRecord CreateEvidence(
        string id = "ev-001",
        string candidateId = "cand-001",
        string evidenceType = "Timeline",
        DateTime collectedAtUtc = default) => new CandidateEvidenceRecord(
            Id: id,
            CandidateId: candidateId,
            EvidenceType: evidenceType,
            ContentJson: "{\"events\":[{\"time\":\"2026-06-12T08:15:00Z\",\"action\":\"ProcessCreate\",\"detail\":\"powershell.exe -enc base64\"}]}",
            CollectedAtUtc: collectedAtUtc);

    private static DapperCandidateEvidenceRepository CreateRepository(out string dbPath)
    {
        dbPath = Path.Combine(
            Path.GetTempPath(),
            $"candidate-evidence-{Guid.NewGuid():N}.db");

        var connectionFactory = new SqliteOperationsDbConnectionFactory(BuildConnectionString(dbPath));
        return new DapperCandidateEvidenceRepository(connectionFactory);
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