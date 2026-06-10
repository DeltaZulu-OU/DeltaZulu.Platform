namespace DeltaZulu.Hunting.Tests;

using DeltaZulu.Hunting.Application.ContentLibrary;
using DeltaZulu.Hunting.Application.SavedQueries;

[TestClass]
public sealed class DetectionContentGovernanceBoundaryTests
{
    [TestMethod]
    [Description("Hunting does not define a parallel accepted detection-content contract before consuming the shared package.")]
    public void ApplicationProject_DoesNotDefineLocalDetectionContentContracts()
    {
        var repositoryRoot = FindRepositoryRoot();
        var localContractDirectory = Path.Combine(repositoryRoot, "src/DeltaZulu.Hunting.Application/DetectionContent");

        Assert.IsFalse(
            Directory.Exists(localContractDirectory),
            "Hunting.Application must not define local DetectionContent contracts; consume the shared DeltaZulu.DetectionContent package instead.");
    }

    [TestMethod]
    [Description("Saved queries remain draft-only local state and are not promoted to accepted detection content by Hunting.")]
    public void SavedQueries_MapToDraftOnlyArtifacts_NotAcceptedContent()
    {
        var timestamp = new DateTime(2026, 6, 9, 1, 2, 3, DateTimeKind.Utc);
        var savedQuery = new SavedQueryRecord(
            "query-1",
            "Suspicious PowerShell",
            "Find encoded commands.",
            "ProcessEvent | where ProcessName == 'powershell.exe'",
            timestamp,
            timestamp,
            LastRunAt: null);

        var artifact = SavedQueryContentLibraryMapper.ToDraftArtifact(savedQuery);

        Assert.AreEqual(ContentLibraryArtifactType.SavedQuery, artifact.Type);
        Assert.AreEqual(ContentLibraryArtifactState.DraftOnly, artifact.State);
        Assert.AreNotEqual(ContentLibraryArtifactType.DetectionQuery, artifact.Type);
        Assert.AreNotEqual(ContentLibraryArtifactState.AcceptedContent, artifact.State);
        Assert.AreEqual("draft-only", artifact.Metadata["governance"]);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DeltaZulu.Platform.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        Assert.Fail("Could not locate repository root from test base directory.");
        return string.Empty;
    }
}
