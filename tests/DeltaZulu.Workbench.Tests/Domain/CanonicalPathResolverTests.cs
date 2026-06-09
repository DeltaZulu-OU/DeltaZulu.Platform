using DeltaZulu.Workbench.Application.ContentPipeline;

namespace DeltaZulu.Workbench.Tests.Domain;

[TestClass]
public sealed class CanonicalPathResolverTests
{
    [TestMethod]
    public void Resolve_DetectionMetadata_ProducesCorrectPath()
    {
        var path = LogicalPath.Parse("detection.yaml");
        var result = CanonicalPathResolver.Resolve("anomalous-sign-in", path);
        Assert.AreEqual("detections/anomalous-sign-in/detection.yaml", result);
    }

    [TestMethod]
    public void Resolve_NestedTestFile_ProducesCorrectPath()
    {
        var path = LogicalPath.Parse("tests/baseline.yaml");
        var result = CanonicalPathResolver.Resolve("anomalous-sign-in", path);
        Assert.AreEqual("detections/anomalous-sign-in/tests/baseline.yaml", result);
    }

    [TestMethod]
    public void Resolve_NoteWithAssets_ProducesCorrectPath()
    {
        var notePath = LogicalPath.Parse("notes/investigation.md");
        var assetPath = LogicalPath.Parse("notes/assets/timeline.png");

        Assert.AreEqual("detections/brute-force-spray/notes/investigation.md",
            CanonicalPathResolver.Resolve("brute-force-spray", notePath));
        Assert.AreEqual("detections/brute-force-spray/notes/assets/timeline.png",
            CanonicalPathResolver.Resolve("brute-force-spray", assetPath));
    }

    [TestMethod]
    public void DetectionPrefix_ReturnsExpectedDirectoryPath()
    {
        Assert.AreEqual("detections/anomalous-sign-in",
            CanonicalPathResolver.DetectionPrefix("anomalous-sign-in"));
    }

    [TestMethod]
    public void ExtractDetectionSlug_FromValidPath_ReturnsSlug()
    {
        Assert.AreEqual("anomalous-sign-in",
            CanonicalPathResolver.ExtractDetectionSlug("detections/anomalous-sign-in/rule.kql"));
    }

    [TestMethod]
    public void ExtractDetectionSlug_FromNestedPath_ReturnsSlug()
    {
        Assert.AreEqual("brute-force-spray",
            CanonicalPathResolver.ExtractDetectionSlug("detections/brute-force-spray/notes/assets/img.png"));
    }

    [TestMethod]
    public void ExtractDetectionSlug_FromNonDetectionPath_ReturnsNull()
    {
        Assert.IsNull(CanonicalPathResolver.ExtractDetectionSlug("README.md"));
    }
}
