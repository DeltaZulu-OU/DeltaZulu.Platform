namespace Workbench.Domain.Enums;

/// <summary>
/// Lifecycle state of a <see cref="Changes.ChangeRequest"/>. Mirrors the state diagram in
/// ARCHITECTURE.md §7. State transitions are enforced by methods on <c>ChangeRequest</c>.
/// </summary>
public enum ChangeStatus
{
    /// <summary>Author is editing draft content. No checks have been run yet, or last run is stale.</summary>
    Draft = 0,

    /// <summary>Check pipeline is currently running.</summary>
    ChecksRunning = 1,

    /// <summary>Submitted to reviewers; required checks have passed.</summary>
    ReviewRequired = 2,

    /// <summary>A reviewer requested changes; back to author.</summary>
    ChangesRequested = 3,

    /// <summary>All gates satisfied; merge is allowed.</summary>
    ReadyToAccept = 4,

    /// <summary>Merged to Git; canonical content committed; version projection created.</summary>
    Merged = 5,

    /// <summary>Closed without merge.</summary>
    Closed = 6,
}