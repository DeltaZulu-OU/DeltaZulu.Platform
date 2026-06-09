using Workbench.Domain.ContentLibrary;

namespace Workbench.Tests.Domain;

[TestClass]
public sealed class ContentLibraryArtifactTests
{
    [TestMethod]
    public void ArtifactModel_SeparatesDraftAcceptedAndRuntimeStates()
    {
        var draft = new ContentLibraryArtifact(
            "saved-query-1",
            ContentLibraryArtifactType.SavedQuery,
            ContentLibraryArtifactState.DraftOnly,
            "queries/saved/signin.kql",
            "Sign-in triage");

        var accepted = draft with
        {
            Type = ContentLibraryArtifactType.DetectionQuery,
            State = ContentLibraryArtifactState.AcceptedContent,
            LogicalPath = "detections/signin/rule.kql",
        };

        var runtime = draft with
        {
            Type = ContentLibraryArtifactType.Visualization,
            State = ContentLibraryArtifactState.RuntimeOnly,
            LogicalPath = "runtime/dashboards/signin.json",
        };

        Assert.AreEqual(ContentLibraryArtifactState.DraftOnly, draft.State);
        Assert.AreEqual(ContentLibraryArtifactState.AcceptedContent, accepted.State);
        Assert.AreEqual(ContentLibraryArtifactState.RuntimeOnly, runtime.State);
    }
}
