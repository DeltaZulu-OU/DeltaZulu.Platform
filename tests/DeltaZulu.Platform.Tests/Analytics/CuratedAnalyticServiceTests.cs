using DeltaZulu.Platform.Application.Analytics.CuratedAnalytics;
using DeltaZulu.Platform.Domain.Analytics;
using DeltaZulu.Platform.Domain.Analytics.CuratedAnalytics;
using DeltaZulu.Platform.Domain.Analytics.SavedQueries;

namespace DeltaZulu.Platform.Tests.Analytics;

[TestClass]
public sealed class CuratedAnalyticServiceTests
{
    [TestMethod]
    public async Task PromoteFromSavedQueryAsync_CreatesRecordFromSavedQuery()
    {
        var now = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);
        var savedQuery = new SavedQueryRecord(
            "sq-1", "Failed Logons", "Detects failures.",
            "SigninLogs | where ResultType != '0'",
            now.AddDays(-1), now.AddHours(-1), now.AddHours(-2));

        var curatedRepo = new StubCuratedRepo();
        var service = new CuratedAnalyticService(curatedRepo, new StubSavedQueryRepo([savedQuery]));

        var result = await service.PromoteFromSavedQueryAsync(
            "sq-1", CuratedAnalyticPurpose.DetectionCandidate, now, TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.AreEqual("Failed Logons", result.Name);
        Assert.AreEqual("Detects failures.", result.Description);
        Assert.AreEqual("SigninLogs | where ResultType != '0'", result.QueryText);
        Assert.AreEqual(CuratedAnalyticPurpose.DetectionCandidate, result.Purpose);
        Assert.AreEqual(now, result.CreatedAt);
        Assert.AreEqual(savedQuery.LastRunAt, result.LastRunAt);
        Assert.IsNull(result.PromotedToDetectionSlug);

        var persisted = await curatedRepo.GetAsync(result.Id, TestContext.CancellationToken);
        Assert.IsNotNull(persisted);
    }

    [TestMethod]
    public async Task PromoteFromSavedQueryAsync_ThrowsWhenSavedQueryNotFound()
    {
        var service = new CuratedAnalyticService(new StubCuratedRepo(), new StubSavedQueryRepo([]));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.PromoteFromSavedQueryAsync(
                "nonexistent", CuratedAnalyticPurpose.General, DateTime.UtcNow, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task PromoteFromSavedQueryAsync_ThrowsOnNullOrWhitespaceId()
    {
        var service = new CuratedAnalyticService(new StubCuratedRepo(), new StubSavedQueryRepo([]));

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => service.PromoteFromSavedQueryAsync(
                " ", CuratedAnalyticPurpose.General, DateTime.UtcNow, TestContext.CancellationToken));
    }

    public TestContext TestContext { get; set; }

    private sealed class StubCuratedRepo : ICuratedAnalyticRepository
    {
        private readonly Dictionary<string, CuratedAnalyticRecord> _store = new();

        public Task EnsureInitializedAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<CuratedAnalyticRecord>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CuratedAnalyticRecord>>(_store.Values.ToArray());
        public Task<PageResult<CuratedAnalyticRecord>> SearchAsync(string? s, int offset, int limit, CancellationToken ct = default)
            => Task.FromResult(new PageResult<CuratedAnalyticRecord>([], 0, offset, limit));
        public Task<CuratedAnalyticRecord?> GetAsync(string id, CancellationToken ct = default)
            => Task.FromResult(_store.GetValueOrDefault(id));
        public Task SaveAsync(CuratedAnalyticRecord record, CancellationToken ct = default)
        { _store[record.Id] = record; return Task.CompletedTask; }
        public Task DeleteAsync(string id, CancellationToken ct = default)
        { _store.Remove(id); return Task.CompletedTask; }
        public Task MarkRunAsync(string id, DateTime runAt, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubSavedQueryRepo(IReadOnlyList<SavedQueryRecord> seed) : ISavedQueryRepository
    {
        private readonly Dictionary<string, SavedQueryRecord> _store = seed.ToDictionary(q => q.Id);

        public Task EnsureInitializedAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<SavedQueryRecord>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SavedQueryRecord>>(_store.Values.ToArray());
        public Task<SavedQueryRecord?> GetAsync(string id, CancellationToken ct = default)
            => Task.FromResult(_store.GetValueOrDefault(id));
        public Task SaveAsync(SavedQueryRecord query, CancellationToken ct = default)
        { _store[query.Id] = query; return Task.CompletedTask; }
        public Task DeleteAsync(string id, CancellationToken ct = default)
        { _store.Remove(id); return Task.CompletedTask; }
        public Task MarkRunAsync(string id, DateTime runAt, CancellationToken ct = default) => Task.CompletedTask;
    }
}
