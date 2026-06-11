namespace DeltaZulu.Platform.Domain.Workbench.ContentLibrary;

/// <summary>Object types supported by the shared detection-content library model.</summary>
public enum ContentLibraryArtifactType
{
    /// <summary>Interactive analyst query saved during authoring.</summary>
    SavedQuery = 1,

    /// <summary>Governed detection or hunting query intended for review and acceptance.</summary>
    DetectionQuery = 2,

    /// <summary>Dashboard, chart, notebook view, or other governed visualization definition.</summary>
    Visualization = 3,

    /// <summary>Fixture data used by deterministic validation tests.</summary>
    Fixture = 4,

    /// <summary>Test/assertion definition attached to accepted or draft content.</summary>
    Test = 5,

    /// <summary>Markdown note, rationale, investigation note, or review-supporting annotation.</summary>
    Note = 6,

    /// <summary>Package metadata that describes a content pack or detection bundle.</summary>
    PackageMetadata = 7,
}