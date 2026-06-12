
using DeltaZulu.Platform.Data.Sqlite.Analytics;
using DeltaZulu.Platform.Data.Sqlite.Analytics.Candidates;
using DeltaZulu.Platform.Domain.Analytics.Candidates;
using Microsoft.Data.Sqlite;

namespace DeltaZulu.Platform.Tests.Analytics;
[TestClass]
public sealed class IncidentCandidateRepositoryTests
{
    [TestMethod]
    public async Task ListAsync_ReturnsEmptyList_WhenStoreIsEmpty()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var candidates = await repository.ListAsync(TestContext.CancellationToken);

            Assert.IsEmpty(candidates);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_PersistsCandidateAcrossRepositoryInstances()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var windowStart = new DateTime(2026, 6, 12, 8, 0, 0, DateTimeKind.Utc);
            var windowEnd = new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc);
            var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);

            await repository.SaveAsync(CreateCandidate(
                id: "cand-001",
                windowStartUtc: windowStart,
                windowEndUtc: windowEnd,
                createdAtUtc: now,
                updatedAtUtc: now), TestContext.CancellationToken);

            var secondRepository = new DapperIncidentCandidateRepository(
                new SqliteAppDbConnectionFactory(BuildConnectionString(dbPath)));

            var saved = await secondRepository.GetAsync("cand-001", TestContext.CancellationToken);

            Assert.IsNotNull(saved);
            Assert.AreEqual("Host", saved.PrimaryEntityType);
            Assert.AreEqual("DC01.contoso.com", saved.PrimaryEntityValue);
            Assert.AreEqual(windowStart, saved.WindowStartUtc);
            Assert.AreEqual(windowEnd, saved.WindowEndUtc);
            Assert.AreEqual(5, saved.AlertCount);
            Assert.AreEqual(3, saved.SourceDiversityCount);
            Assert.AreEqual(2, saved.TacticBreadth);
            Assert.AreEqual(4, saved.TechniqueBreadth);
            Assert.AreEqual(82.5, saved.AggregateRiskScore);
            Assert.AreEqual("Pending", saved.Status);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task ListByStatusAsync_FiltersByStatus()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);

            await repository.SaveAsync(CreateCandidate(id: "cand-001", status: "Pending", createdAtUtc: now, updatedAtUtc: now), TestContext.CancellationToken);
            await repository.SaveAsync(CreateCandidate(id: "cand-002", status: "Approved", createdAtUtc: now, updatedAtUtc: now), TestContext.CancellationToken);
            await repository.SaveAsync(CreateCandidate(id: "cand-003", status: "Pending", createdAtUtc: now, updatedAtUtc: now), TestContext.CancellationToken);

            var pending = await repository.ListByStatusAsync("Pending", TestContext.CancellationToken);

            Assert.HasCount(2, pending);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task ListByEntityAsync_FiltersByPrimaryEntity()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);

            await repository.SaveAsync(CreateCandidate(id: "cand-001",
                primaryEntityType: "Host", primaryEntityValue: "DC01.contoso.com",
                createdAtUtc: now, updatedAtUtc: now), TestContext.CancellationToken);

            await repository.SaveAsync(CreateCandidate(id: "cand-002",
                primaryEntityType: "User", primaryEntityValue: "admin@contoso.com",
                createdAtUtc: now, updatedAtUtc: now), TestContext.CancellationToken);

            var hostCandidates = await repository.ListByEntityAsync("Host", "DC01.contoso.com", TestContext.CancellationToken);

            Assert.HasCount(1, hostCandidates);
            Assert.AreEqual("cand-001", hostCandidates[0].Id);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task UpdateStatusAsync_ChangesStatusAndTimestamp()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);
            var later = new DateTime(2026, 6, 12, 11, 0, 0, DateTimeKind.Utc);

            await repository.SaveAsync(CreateCandidate(
                id: "cand-001", createdAtUtc: now, updatedAtUtc: now), TestContext.CancellationToken);

            await repository.UpdateStatusAsync("cand-001", "Approved", later, TestContext.CancellationToken);

            var saved = await repository.GetAsync("cand-001", TestContext.CancellationToken);

            Assert.IsNotNull(saved);
            Assert.AreEqual("Approved", saved.Status);
            Assert.AreEqual(later, saved.UpdatedAtUtc);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task ListAsync_OrdersByRiskScoreDescending()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);

            await repository.SaveAsync(CreateCandidate(id: "cand-low", aggregateRiskScore: 25.0, createdAtUtc: now, updatedAtUtc: now), TestContext.CancellationToken);
            await repository.SaveAsync(CreateCandidate(id: "cand-high", aggregateRiskScore: 95.0, createdAtUtc: now, updatedAtUtc: now), TestContext.CancellationToken);
            await repository.SaveAsync(CreateCandidate(id: "cand-mid", aggregateRiskScore: 60.0, createdAtUtc: now, updatedAtUtc: now), TestContext.CancellationToken);

            var candidates = await repository.ListAsync(TestContext.CancellationToken);

            Assert.HasCount(3, candidates);
            Assert.AreEqual("cand-high", candidates[0].Id);
            Assert.AreEqual("cand-mid", candidates[1].Id);
            Assert.AreEqual("cand-low", candidates[2].Id);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveAlertLinksAsync_PersistsAndRetrievesLinks()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);
            await repository.SaveAsync(CreateCandidate(id: "cand-001", createdAtUtc: now, updatedAtUtc: now), TestContext.CancellationToken);

            var links = new[]
            {
                new CandidateAlertLink("cand-001", "alert-001", "Entity match: admin@contoso.com within 1h window"),
                new CandidateAlertLink("cand-001", "alert-002", "Entity match: admin@contoso.com within 1h window"),
                new CandidateAlertLink("cand-001", "alert-003", "Technique overlap: T1059.001")
            };

            await repository.SaveAlertLinksAsync(links, TestContext.CancellationToken);

            var saved = await repository.ListAlertLinksAsync("cand-001", TestContext.CancellationToken);

            Assert.HasCount(3, saved);
            Assert.AreEqual("alert-001", saved[0].AlertId);
            Assert.AreEqual("alert-002", saved[1].AlertId);
            Assert.AreEqual("alert-003", saved[2].AlertId);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveAlertLinksAsync_IgnoresDuplicateLinks()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);
            await repository.SaveAsync(CreateCandidate(id: "cand-001", createdAtUtc: now, updatedAtUtc: now), TestContext.CancellationToken);

            var batch1 = new[] { new CandidateAlertLink("cand-001", "alert-001", "First reason") };
            var batch2 = new[] { new CandidateAlertLink("cand-001", "alert-001", "Second reason") };

            await repository.SaveAlertLinksAsync(batch1, TestContext.CancellationToken);
            await repository.SaveAlertLinksAsync(batch2, TestContext.CancellationToken);

            var saved = await repository.ListAlertLinksAsync("cand-001", TestContext.CancellationToken);

            Assert.HasCount(1, saved);
            Assert.AreEqual("First reason", saved[0].ContributionReason);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task UpsertOnConflict_UpdatesMutableFields()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);
            var later = new DateTime(2026, 6, 12, 11, 0, 0, DateTimeKind.Utc);

            await repository.SaveAsync(CreateCandidate(
                id: "cand-001", alertCount: 3, aggregateRiskScore: 50.0,
                createdAtUtc: now, updatedAtUtc: now), TestContext.CancellationToken);

            await repository.SaveAsync(CreateCandidate(
                id: "cand-001", alertCount: 7, aggregateRiskScore: 85.0,
                status: "Approved", createdAtUtc: now, updatedAtUtc: later), TestContext.CancellationToken);

            var saved = await repository.GetAsync("cand-001", TestContext.CancellationToken);

            Assert.IsNotNull(saved);
            Assert.AreEqual(7, saved.AlertCount);
            Assert.AreEqual(85.0, saved.AggregateRiskScore);
            Assert.AreEqual("Approved", saved.Status);
            Assert.AreEqual(now, saved.CreatedAtUtc);
            Assert.AreEqual(later, saved.UpdatedAtUtc);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    private static IncidentCandidateRecord CreateCandidate(
        string id = "cand-001",
        string primaryEntityType = "Host",
        string primaryEntityValue = "DC01.contoso.com",
        DateTime windowStartUtc = default,
        DateTime windowEndUtc = default,
        int alertCount = 5,
        int sourceDiversityCount = 3,
        int tacticBreadth = 2,
        int techniqueBreadth = 4,
        double aggregateRiskScore = 82.5,
        string status = "Pending",
        DateTime createdAtUtc = default,
        DateTime updatedAtUtc = default) => new IncidentCandidateRecord(
            Id: id,
            PrimaryEntityType: primaryEntityType,
            PrimaryEntityValue: primaryEntityValue,
            WindowStartUtc: windowStartUtc,
            WindowEndUtc: windowEndUtc,
            AlertCount: alertCount,
            SourceDiversityCount: sourceDiversityCount,
            TacticBreadth: tacticBreadth,
            TechniqueBreadth: techniqueBreadth,
            AggregateRiskScore: aggregateRiskScore,
            ScoringFactorsJson: "{\"entityWeight\":0.4,\"diversityWeight\":0.3,\"breadthWeight\":0.3}",
            CorrelationRationale: "5 alerts from 3 sources targeting DC01.contoso.com within 1h window, spanning 2 tactics and 4 techniques",
            Status: status,
            CreatedAtUtc: createdAtUtc,
            UpdatedAtUtc: updatedAtUtc);

    private static DapperIncidentCandidateRepository CreateRepository(out string dbPath)
    {
        dbPath = Path.Combine(
            Path.GetTempPath(),
            $"incident-candidates-{Guid.NewGuid():N}.db");

        var connectionFactory = new SqliteAppDbConnectionFactory(BuildConnectionString(dbPath));
        return new DapperIncidentCandidateRepository(connectionFactory);
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
