namespace DeltaZulu.Workbench.Domain.ContentLibrary;

/// <summary>
/// Immutable description of a governed content-library object. Workbench stores draft
/// artifacts in operational persistence and writes accepted artifacts to the canonical
/// Git-backed library layout.
/// </summary>
public sealed record ContentLibraryArtifact(
    string Id,
    ContentLibraryArtifactType Type,
    ContentLibraryArtifactState State,
    string LogicalPath,
    string DisplayName,
    string? DetectionId = null);
