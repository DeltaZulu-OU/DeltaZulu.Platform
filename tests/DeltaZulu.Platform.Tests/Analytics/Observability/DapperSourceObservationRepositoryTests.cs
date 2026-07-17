using System.Data.Common;
using DeltaZulu.Platform.Data.Sqlite.Analytics;
using DeltaZulu.Platform.Data.Sqlite.Analytics.Observability;
using DeltaZulu.Platform.Domain.Analytics.Observability;

namespace DeltaZulu.Platform.Tests.Analytics.Observability;

[TestClass]
public sealed class DapperSourceObservationRepositoryTests : IDisposable
{
    private DbConnection _keepAliveConnection = null!;
    private DapperSourceObservationRepository _repository = null!;
    private bool disposedValue;

    [TestInitialize]
    public void Setup()
    {
        var databaseName = $"SourceObsTest_{Guid.NewGuid():N}";
        var factory = new SqliteAppDbConnectionFactory($"Data Source={databaseName};Mode=Memory;Cache=Shared");
        _keepAliveConnection = factory.CreateConnection();
        _keepAliveConnection.Open();
        _repository = new DapperSourceObservationRepository(factory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _repository.Dispose();
        _keepAliveConnection.Dispose();
    }

    [TestMethod]
    public async Task ListLatest_ReturnsEmpty_WhenNoData()
    {
        var result = await _repository.ListLatestAsync(TestContext.CancellationToken);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task Upsert_ThenList_ReturnsInsertedSnapshot()
    {
        var snapshot = MakeSnapshot("WindowsEventLog", "Security", "agent-01");
        await _repository.UpsertAsync(snapshot, TestContext.CancellationToken);

        var result = await _repository.ListLatestAsync(TestContext.CancellationToken);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("WindowsEventLog", result[0].SourceType);
        Assert.AreEqual("Security", result[0].Channel);
        Assert.AreEqual("agent-01", result[0].AgentId);
        Assert.AreEqual(1000, result[0].ReadCount);
        Assert.IsTrue(result[0].CanRead);
    }

    [TestMethod]
    public async Task Upsert_UpdatesExistingRow_OnConflict()
    {
        var first = MakeSnapshot("WindowsEventLog", "Security", "agent-01", readCount: 500);
        await _repository.UpsertAsync(first, TestContext.CancellationToken);

        var updated = MakeSnapshot("WindowsEventLog", "Security", "agent-01", readCount: 1500);
        await _repository.UpsertAsync(updated, TestContext.CancellationToken);

        var result = await _repository.ListLatestAsync(TestContext.CancellationToken);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(1500, result[0].ReadCount);
    }

    [TestMethod]
    public async Task Upsert_KeepsSeparateRows_ForDifferentAgents()
    {
        await _repository.UpsertAsync(MakeSnapshot("WindowsEventLog", "Security", "agent-01"), TestContext.CancellationToken);
        await _repository.UpsertAsync(MakeSnapshot("WindowsEventLog", "Security", "agent-02"), TestContext.CancellationToken);

        var result = await _repository.ListLatestAsync(TestContext.CancellationToken);
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task Upsert_KeepsSeparateRows_ForDifferentChannels()
    {
        await _repository.UpsertAsync(MakeSnapshot("WindowsEventLog", "Security", "agent-01"), TestContext.CancellationToken);
        await _repository.UpsertAsync(MakeSnapshot("WindowsEventLog", "Sysmon/Operational", "agent-01"), TestContext.CancellationToken);

        var result = await _repository.ListLatestAsync(TestContext.CancellationToken);
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task Roundtrip_PreservesAllFields()
    {
        var now = DateTime.UtcNow;
        var snapshot = new SourceObservationSnapshot(
            "DNSServer", "Analytical", "agent-dc01", "dc01.corp.local",
            IsEnabled: true, CanRead: true, LastReadAtUtc: now,
            ReadErrorCount: 3, LastError: "Transient bookmark error",
            ReadCount: 5000, KeptAfterFilterCount: 4500, DiscardedCount: 500,
            ForwardedCount: 4400, ForwardFailedCount: 100, ObservedAtUtc: now);

        await _repository.UpsertAsync(snapshot, TestContext.CancellationToken);
        var result = (await _repository.ListLatestAsync(TestContext.CancellationToken))[0];

        Assert.AreEqual("DNSServer", result.SourceType);
        Assert.AreEqual("Analytical", result.Channel);
        Assert.AreEqual("agent-dc01", result.AgentId);
        Assert.AreEqual("dc01.corp.local", result.HostId);
        Assert.IsTrue(result.IsEnabled);
        Assert.IsTrue(result.CanRead);
        Assert.AreEqual(3, result.ReadErrorCount);
        Assert.AreEqual("Transient bookmark error", result.LastError);
        Assert.AreEqual(5000, result.ReadCount);
        Assert.AreEqual(4500, result.KeptAfterFilterCount);
        Assert.AreEqual(500, result.DiscardedCount);
        Assert.AreEqual(4400, result.ForwardedCount);
        Assert.AreEqual(100, result.ForwardFailedCount);
    }

    private static SourceObservationSnapshot MakeSnapshot(
        string sourceType = "WindowsEventLog",
        string channel = "Security",
        string agentId = "agent-01",
        long readCount = 1000) =>
        new(sourceType, channel, agentId, "host-01",
            IsEnabled: true, CanRead: true, LastReadAtUtc: DateTime.UtcNow,
            ReadErrorCount: 0, LastError: null,
            ReadCount: readCount, KeptAfterFilterCount: readCount, DiscardedCount: 0,
            ForwardedCount: readCount, ForwardFailedCount: 0, ObservedAtUtc: DateTime.UtcNow);

    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _keepAliveConnection?.Dispose();
                _repository?.Dispose();
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public TestContext TestContext { get; set; }
}
