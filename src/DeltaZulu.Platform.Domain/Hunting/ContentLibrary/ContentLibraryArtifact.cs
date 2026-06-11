namespace DeltaZulu.Platform.Application.Hunting.ContentLibrary;

public sealed record ContentLibraryArtifact(
    string Id,
    ContentLibraryArtifactType Type,
    ContentLibraryArtifactState State,
    string Name,
    string? Description,
    string Body,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public enum ContentLibraryArtifactType
{
    SavedQuery,
    DetectionQuery,
    Visualization,
    Fixture,
    Test,
    Note,
    PackageMetadata
}

public enum ContentLibraryArtifactState
{
    DraftOnly,
    AcceptedContent,
    RuntimeOnly
}