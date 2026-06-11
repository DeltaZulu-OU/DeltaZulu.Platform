namespace DeltaZulu.Platform.Domain.Workbench.Enums;

/// <summary>
/// Lifecycle of a <see cref="Detections.Detection"/> aggregate. A detection can exist in the
/// database before accepted content exists so that changes can target it before the first
/// merge; it only becomes <see cref="Accepted"/> when an initial change merges.
/// </summary>
public enum DetectionLifecycle
{
    /// <summary>Draft identity in the database; has no accepted version in Git yet.</summary>
    Draft = 0,

    /// <summary>At least one change has merged; canonical content exists in Git.</summary>
    Accepted = 1,

    /// <summary>Deprecated; retained for history but not for new changes.</summary>
    Deprecated = 2,
}