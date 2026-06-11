using DeltaZulu.Platform.Application.Governance.ContentLibrary;
using DeltaZulu.Platform.Domain.Governance.ContentLibrary;

namespace DeltaZulu.Platform.Tests.Governance.Application;

[TestClass]
public sealed class SavedQueryImporterTests
{
    [TestMethod]
    public void Convert_WithDetectionSlug_ProducesDraftOnlySavedQueryAndRuleDraft()
    {
        var importer = new SavedQueryImporter();

        var results = importer.Convert(new[]
        {
            new SavedQueryExportRecord(
                SourceId: "sq-100",
                DisplayName: "Suspicious Sign-in Query",
                QueryText: "SecurityEvent | take 10",
                DetectionSlug: "suspicious-sign-in"),
        });

        var result = results.Single();
        Assert.AreEqual("hunting-saved-query:sq-100", result.Artifact.Id);
        Assert.AreEqual(ContentLibraryArtifactType.SavedQuery, result.Artifact.Type);
        Assert.AreEqual(ContentLibraryArtifactState.DraftOnly, result.Artifact.State);
        Assert.AreEqual("draft/saved-queries/suspicious-sign-in-query.kql", result.Artifact.LogicalPath);
        Assert.AreEqual("suspicious-sign-in", result.Artifact.DetectionId);
        Assert.AreEqual("rule.kql", result.DraftFiles.Single().LogicalPath);
        Assert.AreEqual(DraftContentType.AnalyticsQuery, result.DraftFiles.Single().ContentType);
        Assert.AreEqual("SecurityEvent | take 10", result.DraftFiles.Single().Content);
        Assert.IsEmpty(result.Warnings);
    }

    [TestMethod]
    public void Convert_DuplicateNames_ProducesUniqueDraftArtifactPaths()
    {
        var importer = new SavedQueryImporter();

        var results = importer.Convert(new[]
        {
            new SavedQueryExportRecord("sq-1", "Repeated Name", "A"),
            new SavedQueryExportRecord("sq-2", "Repeated Name", "B"),
        });

        Assert.AreEqual("draft/saved-queries/repeated-name.kql", results[0].Artifact.LogicalPath);
        Assert.AreEqual("draft/saved-queries/repeated-name-2.kql", results[1].Artifact.LogicalPath);
        Assert.HasCount(2, results.Select(r => r.Artifact.LogicalPath).Distinct());
    }

    [TestMethod]
    public void Convert_EmptyQuery_PreservesDraftAndReturnsWarning()
    {
        var importer = new SavedQueryImporter();

        var result = importer.Convert(new[]
        {
            new SavedQueryExportRecord("sq-empty", "Empty Query", string.Empty),
        }).Single();

        Assert.AreEqual(string.Empty, result.DraftFiles.Single().Content);
        Assert.Contains("content is empty", result.Warnings[0]);
        Assert.Contains("No detection slug supplied", result.Warnings[1]);
    }
}