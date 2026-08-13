using DeltaZulu.Platform.Domain.Analytics.Observability;

namespace DeltaZulu.Platform.Tests.Analytics.Observability;

[TestClass]
public sealed class SourceObservationSnapshotTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    [TestMethod]
    public void HealthStatus_IsHealthy_WhenEnabledReadableNoErrors()
    {
        var snapshot = MakeSnapshot(isEnabled: true, canRead: true, readErrorCount: 0, forwardFailedCount: 0, readCount: 100);
        Assert.AreEqual(SourceHealthStatus.Healthy, snapshot.HealthStatus);
    }

    [TestMethod]
    public void HealthStatus_IsDegraded_WhenReadErrors()
    {
        var snapshot = MakeSnapshot(isEnabled: true, canRead: true, readErrorCount: 3, forwardFailedCount: 0, readCount: 100);
        Assert.AreEqual(SourceHealthStatus.Degraded, snapshot.HealthStatus);
    }

    [TestMethod]
    public void HealthStatus_IsDegraded_WhenCannotRead()
    {
        var snapshot = MakeSnapshot(isEnabled: true, canRead: false, readErrorCount: 0, forwardFailedCount: 0, readCount: 0);
        Assert.AreEqual(SourceHealthStatus.Degraded, snapshot.HealthStatus);
    }

    [TestMethod]
    public void HealthStatus_IsDegraded_WhenForwardFailures()
    {
        var snapshot = MakeSnapshot(isEnabled: true, canRead: true, readErrorCount: 0, forwardFailedCount: 5, readCount: 100);
        Assert.AreEqual(SourceHealthStatus.Degraded, snapshot.HealthStatus);
    }

    [TestMethod]
    public void HealthStatus_IsDisabled_WhenNotEnabled()
    {
        var snapshot = MakeSnapshot(isEnabled: false, canRead: false, readErrorCount: 0, forwardFailedCount: 0, readCount: 0);
        Assert.AreEqual(SourceHealthStatus.Disabled, snapshot.HealthStatus);
    }

    [TestMethod]
    public void HealthStatus_IsInactive_WhenEnabledReadableButZeroReads()
    {
        var snapshot = MakeSnapshot(isEnabled: true, canRead: true, readErrorCount: 0, forwardFailedCount: 0, readCount: 0);
        Assert.AreEqual(SourceHealthStatus.Inactive, snapshot.HealthStatus);
    }

    [TestMethod]
    public void DiscardRatio_IsZero_WhenNoReads()
    {
        var snapshot = MakeSnapshot(readCount: 0, discardedCount: 0);
        Assert.AreEqual(0, snapshot.DiscardRatio);
    }

    [TestMethod]
    public void DiscardRatio_IsCorrect_WhenReadsExist()
    {
        var snapshot = MakeSnapshot(readCount: 200, discardedCount: 50);
        Assert.AreEqual(0.25, snapshot.DiscardRatio, 0.001);
    }

    private static SourceObservationSnapshot MakeSnapshot(
        bool isEnabled = true,
        bool canRead = true,
        long readErrorCount = 0,
        long forwardFailedCount = 0,
        long readCount = 100,
        long discardedCount = 0) =>
        new("WindowsEventLog", "Security", "agent-01", "host-01",
            isEnabled, canRead, Now, readErrorCount, null,
            readCount, readCount - discardedCount, discardedCount,
            readCount, forwardFailedCount, Now);
}
