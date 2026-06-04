namespace Workbench.Domain.Enums;

/// <summary>
/// Lifecycle of a <see cref="Detections.Detection"/> aggregate. A detection exists in the
/// database from the moment it is conceived (so that issues and changes can be linked to it
/// before first merge); it only becomes <see cref="Accepted"/> when an initial change merges.
/// </summary>
public enum DetectionLifecycle
{
    /// <summary>Created in the database; has no accepted version in Git yet.</summary>
    Conceived = 0,

    /// <summary>At least one change has merged; canonical content exists in Git.</summary>
    Accepted = 1,

    /// <summary>Deprecated; retained for history but not for new changes.</summary>
    Deprecated = 2,
}