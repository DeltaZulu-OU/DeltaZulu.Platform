namespace DeltaZulu.Platform.Tests.Hunting;

using DeltaZulu.Platform.Application.Hunting.ContentLibrary;
using DeltaZulu.Platform.Application.Hunting.SavedQueries;

[TestClass]
public sealed class SavedQueryContentLibraryMapperTests
{
    [TestMethod]
    [Description("Existing Hunting saved queries map to draft-only SavedQuery content-library artifacts.")]
    public void ToDraftArtifact_MapsSavedQueryToDraftOnlyArtifact()
    {
        var createdAt = new DateTime(2026, 01, 02, 03, 04, 05, DateTimeKind.Utc);
        var updatedAt = createdAt.AddHours(1);
        var lastRunAt = updatedAt.AddMinutes(5);
        var savedQuery = new SavedQueryRecord(
            "query-1",
            "Suspicious PowerShell",
            "Find encoded commands.",
            "ProcessEvent | where ProcessName == 'powershell.exe'",
            createdAt,
            updatedAt,
            lastRunAt);

        var artifact = SavedQueryContentLibraryMapper.ToDraftArtifact(savedQuery);

        Assert.AreEqual("query-1", artifact.Id);
        Assert.AreEqual(ContentLibraryArtifactType.SavedQuery, artifact.Type);
        Assert.AreEqual(ContentLibraryArtifactState.DraftOnly, artifact.State);
        Assert.AreEqual(savedQuery.Name, artifact.Name);
        Assert.AreEqual(savedQuery.Description, artifact.Description);
        Assert.AreEqual(savedQuery.QueryText, artifact.Body);
        Assert.AreEqual("hunting.saved-query", artifact.Metadata["source"]);
        Assert.AreEqual("draft-only", artifact.Metadata["governance"]);
        Assert.AreEqual(lastRunAt.ToString("O"), artifact.Metadata["lastRunAtUtc"]);
    }
}