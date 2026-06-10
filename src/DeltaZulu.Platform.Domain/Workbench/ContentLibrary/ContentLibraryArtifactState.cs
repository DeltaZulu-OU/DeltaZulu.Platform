namespace DeltaZulu.Workbench.Domain.ContentLibrary;

/// <summary>Separates editor conveniences from Git-backed accepted content and runtime-only state.</summary>
public enum ContentLibraryArtifactState
{
    /// <summary>Database-owned working copy that is not accepted content.</summary>
    DraftOnly = 1,

    /// <summary>Accepted canonical content written to Git after governance gates pass.</summary>
    AcceptedContent = 2,

    /// <summary>Runtime/operator object that is not committed as accepted content.</summary>
    RuntimeOnly = 3,
}