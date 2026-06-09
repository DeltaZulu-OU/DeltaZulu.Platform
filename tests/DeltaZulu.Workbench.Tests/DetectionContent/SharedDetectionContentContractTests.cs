using DeltaZulu.DetectionContent;
using DeltaZulu.DetectionContent.Files;
using DeltaZulu.DetectionContent.Identity;
using DeltaZulu.DetectionContent.Paths;
using DeltaZulu.DetectionContent.References;
using DeltaZulu.Workbench.Application.ContentPipeline;
using DeltaZulu.Workbench.Domain.Identifiers;

namespace DeltaZulu.Workbench.Tests.DetectionContent;

[TestClass]
public sealed class SharedDetectionContentContractTests
{
    [TestMethod]
    public void DetectionContentPathResolver_UsesCanonicalAcceptedDetectionLayout()
    {
        var slug = DetectionSlug.Parse("anomalous-sign-in");
        var path = DetectionLogicalPath.Parse("tests/baseline.yaml");

        var repositoryPath = DetectionContentPathResolver.Resolve(slug, path);

        Assert.AreEqual("detections/anomalous-sign-in/tests/baseline.yaml", repositoryPath);
        Assert.AreEqual("anomalous-sign-in", DetectionContentPathResolver.ExtractDetectionSlug(repositoryPath)?.Value);
    }

    [TestMethod]
    public void DetectionContentPathResolver_TryExtractDetectionSlug_RejectsInvalidSlugShape()
    {
        var extracted = DetectionContentPathResolver.TryExtractDetectionSlug(
            "detections/Unsafe Slug/rule.kql",
            out var slug);

        Assert.IsFalse(extracted);
        Assert.IsNull(slug);
    }

    [TestMethod]
    public void WorkbenchDetectionId_MapsToSharedDetectionContentIdByValue()
    {
        var workbenchId = DetectionId.New();

        var sharedId = DetectionContentId.From(workbenchId.Value);

        Assert.AreEqual(workbenchId.Value, sharedId.Value);
    }

    [TestMethod]
    public void DetectionContentId_RejectsEmptyGuid()
    {
        var exception = Assert.ThrowsExactly<DetectionContentException>(() =>
            DetectionContentId.From(Guid.Empty));

        Assert.AreEqual("detection_content_id.empty", exception.Code);
    }

    [TestMethod]
    public void DetectionContentVersionId_RejectsEmptyGuid()
    {
        var exception = Assert.ThrowsExactly<DetectionContentException>(() =>
            DetectionContentVersionId.From(Guid.Empty));

        Assert.AreEqual("detection_content_version_id.empty", exception.Code);
    }

    [TestMethod]
    public void AcceptedDetectionContentRef_RejectsNullSlug()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new AcceptedDetectionContentRef(DetectionContentId.New(), null!, null, null));
    }

    [TestMethod]
    public void DetectionContentFile_RejectsTraversalRepositoryPath()
    {
        var exception = Assert.ThrowsExactly<DetectionContentException>(() =>
            new DetectionContentFile("detections/anomalous-sign-in/../rule.kql", "SigninLogs | take 1"));

        Assert.AreEqual("path.traversal", exception.Code);
    }

    [TestMethod]
    public void DetectionLogicalPath_RejectsTraversalOutsidePackage()
    {
        var exception = Assert.ThrowsExactly<DetectionContentException>(() =>
            DetectionLogicalPath.Parse("../rule.kql"));

        Assert.AreEqual("path.traversal", exception.Code);
    }

    [TestMethod]
    public void DetectionSlug_RejectsUnsafeSlugShape()
    {
        var exception = Assert.ThrowsExactly<DetectionContentException>(() =>
            DetectionSlug.Parse("Unsafe Slug"));

        Assert.AreEqual("detection.slug_invalid", exception.Code);
    }

    [TestMethod]
    public void CanonicalPathResolver_RemainsCompatibleWithExistingLogicalPaths()
    {
        var logicalPaths = new[]
        {
            "detection.yaml",
            "rule.kql",
            "tests/baseline.yaml",
            "fixtures/sign-in.ndjson",
            "notes/investigation.md",
            "notes/assets/timeline.png",
        };

        foreach (var logicalPath in logicalPaths)
        {
            var path = LogicalPath.Parse(logicalPath);
            var repositoryPath = CanonicalPathResolver.Resolve("anomalous-sign-in", path);

            Assert.AreEqual($"detections/anomalous-sign-in/{logicalPath}", repositoryPath);
        }
    }
}
