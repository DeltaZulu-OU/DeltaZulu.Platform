using DeltaZulu.Platform.Domain.Detection.Files;
using DeltaZulu.Platform.Domain.Detection.Identity;
using DeltaZulu.Platform.Domain.Detection.Paths;
using DeltaZulu.Platform.Domain.Detection.References;

namespace DeltaZulu.Platform.Tests.Detection;

[TestClass]
public sealed class DetectionContentContractTests
{
    [TestMethod]
    public void DetectionContentId_RejectsEmptyGuidAndUsesCanonicalString()
    {
        var value = Guid.Parse("1f743735-83cb-447c-b25c-a3fb2f85262f");

        var id = DetectionContentId.From(value);

        Assert.AreEqual(value, id.Value);
        Assert.AreEqual("1f743735-83cb-447c-b25c-a3fb2f85262f", id.ToString());
        AssertContractError("detection_content_id.empty", () => DetectionContentId.From(Guid.Empty));
        AssertContractError("detection_content_id.invalid", () => DetectionContentId.Parse("not-a-guid"));
    }

    [TestMethod]
    public void DetectionContentVersionId_RejectsEmptyGuidAndUsesCanonicalString()
    {
        var value = Guid.Parse("f3ff98c3-3ff7-4444-8843-42ff97be26dc");

        var id = DetectionContentVersionId.Parse(value.ToString("N"));

        Assert.AreEqual(value, id.Value);
        Assert.AreEqual("f3ff98c3-3ff7-4444-8843-42ff97be26dc", id.ToString());
        AssertContractError("detection_content_version_id.empty", () => DetectionContentVersionId.From(Guid.Empty));
        AssertContractError("detection_content_version_id.invalid", () => DetectionContentVersionId.Parse("not-a-guid"));
    }

    [TestMethod]
    [DataRow("abc")]
    [DataRow("query-library-01")]
    [DataRow("a1-b2-c3")]
    public void DetectionSlug_AcceptsPathSafeSlugs(string raw)
    {
        var slug = DetectionSlug.Parse(raw);

        Assert.AreEqual(raw, slug.Value);
        Assert.AreEqual(raw, slug.ToString());
    }

    [TestMethod]
    [DataRow("ab")]
    [DataRow("Uppercase")]
    [DataRow("starts-")]
    [DataRow("-starts")]
    [DataRow("has_underscore")]
    public void DetectionSlug_RejectsInvalidSlugs(string raw) =>
        AssertContractError("detection.slug_invalid", () => DetectionSlug.Parse(raw));

    [TestMethod]
    public void DetectionLogicalPath_RejectsTraversalAbsoluteBackslashAndUnsafeSegments()
    {
        Assert.AreEqual("queries/active-users.kql", DetectionLogicalPath.Parse("queries/active-users.kql").Value);

        AssertContractError("path.empty", () => DetectionLogicalPath.Parse(" "));
        AssertContractError("path.backslash", () => DetectionLogicalPath.Parse("queries\\file.kql"));
        AssertContractError("path.boundary_slash", () => DetectionLogicalPath.Parse("/queries/file.kql"));
        AssertContractError("path.boundary_slash", () => DetectionLogicalPath.Parse("queries/file.kql/"));
        AssertContractError("path.empty_segment", () => DetectionLogicalPath.Parse("queries//file.kql"));
        AssertContractError("path.traversal", () => DetectionLogicalPath.Parse("queries/../file.kql"));
        AssertContractError("path.segment_chars", () => DetectionLogicalPath.Parse("queries/file name.kql"));
    }

    [TestMethod]
    public void DetectionRepositoryPath_ParsesSlugAndLogicalPath()
    {
        var path = DetectionRepositoryPath.Parse("detections/login-anomaly/queries/main.kql");

        Assert.AreEqual("detections/login-anomaly/queries/main.kql", path.Value);
        Assert.AreEqual("login-anomaly", path.Slug.Value);
        Assert.AreEqual("queries/main.kql", path.LogicalPath.Value);
    }

    [TestMethod]
    [DataRow("other/login-anomaly/queries/main.kql", "repository_path.root")]
    [DataRow("/detections/login-anomaly/queries/main.kql", "repository_path.boundary_slash")]
    [DataRow("detections/login-anomaly/", "repository_path.boundary_slash")]
    [DataRow("detections/login-anomaly", "repository_path.shape")]
    [DataRow("detections/login-anomaly\\queries\\main.kql", "repository_path.backslash")]
    public void DetectionRepositoryPath_RejectsPathsOutsideAcceptedConvention(string raw, string code) =>
        AssertContractError(code, () => DetectionRepositoryPath.Parse(raw));

    [TestMethod]
    public void DetectionContentPathResolver_RoundTripsRepositoryConventions()
    {
        var slug = DetectionSlug.Parse("login-anomaly");
        var logicalPath = DetectionLogicalPath.Parse("queries/main.kql");

        var resolved = DetectionContentPathResolver.Resolve(slug, logicalPath);
        var prefix = DetectionContentPathResolver.DetectionPrefix(slug);
        var extracted = DetectionContentPathResolver.ExtractDetectionSlug(resolved);

        Assert.AreEqual("detections/login-anomaly/queries/main.kql", resolved);
        Assert.AreEqual("detections/login-anomaly", prefix);
        Assert.IsNotNull(extracted);
        Assert.AreEqual("login-anomaly", extracted.Value);
        Assert.IsFalse(DetectionContentPathResolver.TryExtractDetectionSlug("detections/login-anomaly", out _));
        Assert.IsNull(DetectionContentPathResolver.ExtractDetectionSlug("other/login-anomaly/queries/main.kql"));
    }

    [TestMethod]
    public void DetectionContentFile_PreservesValidatedPathAndPayloadMetadata()
    {
        var file = new DetectionContentFile("detections/login-anomaly/queries/main.kql", "SecurityEvent | take 10", isBinary: false);

        Assert.AreEqual("detections/login-anomaly/queries/main.kql", file.RepositoryPath);
        Assert.AreEqual("login-anomaly", file.Path.Slug.Value);
        Assert.AreEqual("SecurityEvent | take 10", file.Content);
        Assert.IsFalse(file.IsBinary);
    }

    [TestMethod]
    public void AcceptedDetectionVersionRef_ValidatesSequenceAndCommitReference()
    {
        var versionId = DetectionContentVersionId.From(Guid.Parse("4b826e83-b117-40b1-8d87-6db8591a2dc2"));
        var detectionId = DetectionContentId.From(Guid.Parse("1f743735-83cb-447c-b25c-a3fb2f85262f"));
        var slug = DetectionSlug.Parse("login-anomaly");
        var acceptedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        var reference = new AcceptedDetectionVersionRef(versionId, detectionId, slug, 3, "v3", acceptedAt, "abc123");

        Assert.AreEqual(3, reference.SequenceNumber);
        Assert.AreEqual("v3", reference.DisplayVersion);
        Assert.AreEqual("abc123", reference.AcceptedContentCommitSha);

        AssertContractError(
            "version.sequence_invalid",
            () => _ = new AcceptedDetectionVersionRef(versionId, detectionId, slug, 0, "v0", acceptedAt, null));

        AssertContractError(
            "accepted_content.commit_empty",
            () => _ = new AcceptedDetectionVersionRef(versionId, detectionId, slug, 1, "v1", acceptedAt, " "));

        AssertContractError(
            "accepted_content.commit_empty",
            () => _ = new AcceptedDetectionContentRef(detectionId, slug, versionId, " "));
    }

    private static void AssertContractError(string expectedCode, Action action)
    {
        var exception = Assert.ThrowsExactly<DetectionContentException>(action);
        Assert.AreEqual(expectedCode, exception.Code);
    }
}