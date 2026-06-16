using DeltaZulu.Platform.Data.Sqlite.Analytics;
using DeltaZulu.Platform.Data.Sqlite.Analytics.CuratedAnalytics;
using DeltaZulu.Platform.Domain.Analytics.CuratedAnalytics;
using Microsoft.Data.Sqlite;

namespace DeltaZulu.Platform.Tests.Analytics;

[TestClass]
public sealed class CuratedAnalyticRepositoryTests
{
    [TestMethod]
    public async Task SaveAsync_PersistsRecordWithAllFields()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var createdAt = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);
            var record = new CuratedAnalyticRecord(
                Id: "ca-1",
                Name: "Failed Logon Spikes",
                Description: "Detect anomalous failed logon volumes.",
                QueryText: "SigninLogs | where ResultType != '0' | summarize Count=count() by bin(TimeGenerated, 1h)",
                Purpose: CuratedAnalyticPurpose.DetectionCandidate,
                RequiredViews: "SigninLogs",
                RequiredFields: "ResultType,TimeGenerated",
                ExpectedResultShape: "TimeGenerated:datetime,Count:long",
                EntityMappingsJson: """[{"entity":"Account","field":"UserPrincipalName"}]""",
                KnownFalsePositives: "Password spray testing periods.",
                SeverityHint: 3,
                ConfidenceHint: 4,
                RiskHint: 3,
                Notes: "Useful for SOC morning review.",
                PromotedToDetectionSlug: null,
                CreatedAt: createdAt,
                UpdatedAt: createdAt,
                LastRunAt: null);

            await repository.SaveAsync(record, TestContext.CancellationToken);

            var loaded = await repository.GetAsync("ca-1", TestContext.CancellationToken);

            Assert.IsNotNull(loaded);
            Assert.AreEqual("Failed Logon Spikes", loaded.Name);
            Assert.AreEqual(CuratedAnalyticPurpose.DetectionCandidate, loaded.Purpose);
            Assert.AreEqual("SigninLogs", loaded.RequiredViews);
            Assert.AreEqual("ResultType,TimeGenerated", loaded.RequiredFields);
            Assert.AreEqual("TimeGenerated:datetime,Count:long", loaded.ExpectedResultShape);
            Assert.IsNotNull(loaded.EntityMappingsJson);
            Assert.AreEqual("Password spray testing periods.", loaded.KnownFalsePositives);
            Assert.AreEqual(3, loaded.SeverityHint);
            Assert.AreEqual(4, loaded.ConfidenceHint);
            Assert.AreEqual(3, loaded.RiskHint);
            Assert.AreEqual("Useful for SOC morning review.", loaded.Notes);
            Assert.IsNull(loaded.PromotedToDetectionSlug);
            Assert.AreEqual(createdAt, loaded.CreatedAt);

            Assert.IsNull(await repository.GetAsync("nonexistent", TestContext.CancellationToken));
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_Upsert_UpdatesExistingRecord()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);
            var original = new CuratedAnalyticRecord(
                "ca-1", "Original", null, "T | take 1",
                CuratedAnalyticPurpose.General,
                null, null, null, null, null, null, null, null, null, null,
                now, now, null);

            await repository.SaveAsync(original, TestContext.CancellationToken);

            var later = now.AddMinutes(5);
            var updated = original with { Name = "Updated", UpdatedAt = later, SeverityHint = 5 };
            await repository.SaveAsync(updated, TestContext.CancellationToken);

            var loaded = await repository.GetAsync("ca-1", TestContext.CancellationToken);
            Assert.IsNotNull(loaded);
            Assert.AreEqual("Updated", loaded.Name);
            Assert.AreEqual(5, loaded.SeverityHint);
            Assert.AreEqual(later, loaded.UpdatedAt);
            Assert.AreEqual(now, loaded.CreatedAt);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task DeleteAsync_RemovesRecord()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = DateTime.UtcNow;
            await repository.SaveAsync(new CuratedAnalyticRecord(
                "ca-1", "Test", null, "T", CuratedAnalyticPurpose.General,
                null, null, null, null, null, null, null, null, null, null,
                now, now, null), TestContext.CancellationToken);

            await repository.DeleteAsync("ca-1", TestContext.CancellationToken);

            Assert.IsNull(await repository.GetAsync("ca-1", TestContext.CancellationToken));
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task MarkRunAsync_UpdatesLastRunAt()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc);
            await repository.SaveAsync(new CuratedAnalyticRecord(
                "ca-1", "Test", null, "T", CuratedAnalyticPurpose.General,
                null, null, null, null, null, null, null, null, null, null,
                now, now, null), TestContext.CancellationToken);

            var runAt = now.AddHours(1);
            await repository.MarkRunAsync("ca-1", runAt, TestContext.CancellationToken);

            var loaded = await repository.GetAsync("ca-1", TestContext.CancellationToken);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(runAt, loaded.LastRunAt);
            Assert.AreEqual(runAt, loaded.UpdatedAt);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    [TestMethod]
    public async Task SearchAsync_FiltersAndPaginates()
    {
        var repository = CreateRepository(out var dbPath);
        try
        {
            var now = DateTime.UtcNow;
            for (var i = 0; i < 5; i++)
            {
                await repository.SaveAsync(new CuratedAnalyticRecord(
                    $"ca-{i}", i < 3 ? $"Logon Analytic {i}" : $"Network Analytic {i}",
                    null, "T | take 1", CuratedAnalyticPurpose.General,
                    null, null, null, null, null, null, null, null, null, null,
                    now.AddMinutes(-i), now.AddMinutes(-i), null), TestContext.CancellationToken);
            }

            var filtered = await repository.SearchAsync("Logon", 0, 10, TestContext.CancellationToken);
            Assert.AreEqual(3, filtered.TotalCount);
            Assert.HasCount(3, filtered.Items);
            Assert.IsTrue(filtered.Items.All(r => r.Name.Contains("Logon", StringComparison.Ordinal)));

            var all = await repository.SearchAsync(null, 0, 10, TestContext.CancellationToken);
            Assert.AreEqual(5, all.TotalCount);
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    public TestContext TestContext { get; set; }

    private static DapperCuratedAnalyticRepository CreateRepository(out string dbPath)
    {
        dbPath = Path.Combine(
            Path.GetTempPath(),
            $"curated-analytics-{Guid.NewGuid():N}.db");

        var connectionFactory = new SqliteAppDbConnectionFactory(BuildConnectionString(dbPath));
        return new DapperCuratedAnalyticRepository(connectionFactory);
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
}