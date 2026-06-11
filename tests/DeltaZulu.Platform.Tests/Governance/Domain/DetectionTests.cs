using static DeltaZulu.Platform.Domain.Governance.Detections.Detection;

namespace DeltaZulu.Platform.Tests.Governance.Domain;

[TestClass]
public sealed class DetectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [DataRow("anomalous-sign-in")]
    [DataRow("a1b2")]
    [DataRow("aws-iam-root-login")]
    public void CreateDraft_AcceptsValidSlugs(string slug)
    {
        var d = CreateDraft(DetectionId.New(), slug, "title", "summary", Now);
        Assert.AreEqual(slug, d.Slug);
        Assert.AreEqual(DetectionLifecycle.Draft, d.Lifecycle);
        Assert.IsNull(d.CurrentVersionId);
    }

    [TestMethod]
    [DataRow("UPPER")]
    [DataRow("-leading-hyphen")]
    [DataRow("trailing-hyphen-")]
    [DataRow("ab")]
    [DataRow("has space")]
    [DataRow("with_underscore")]
    public void CreateDraft_RejectsInvalidSlugs(string slug)
    {
        var ex = Assert.ThrowsExactly<DomainException>(
            () => CreateDraft(DetectionId.New(), slug, "title", "summary", Now));
        Assert.AreEqual("detection.slug_invalid", ex.Code);
    }

    [TestMethod]
    public void MarkAccepted_PromotesLifecycleAndRecordsVersion()
    {
        var d = CreateDraft(DetectionId.New(), "demo", "Title", "Summary", Now);
        var v = VersionId.New();

        d.MarkAccepted(v, Now.AddMinutes(5));

        Assert.AreEqual(DetectionLifecycle.Accepted, d.Lifecycle);
        Assert.AreEqual(v, d.CurrentVersionId);
    }

    [TestMethod]
    public void MarkAccepted_OnDeprecatedDetection_Throws()
    {
        var d = CreateDraft(DetectionId.New(), "demo", "Title", "Summary", Now);
        d.Deprecate(Now);

        var ex = Assert.ThrowsExactly<DomainException>(() => d.MarkAccepted(VersionId.New(), Now));
        Assert.AreEqual("detection.deprecated_no_accept", ex.Code);
    }
}