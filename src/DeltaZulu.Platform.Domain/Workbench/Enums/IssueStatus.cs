namespace DeltaZulu.Platform.Domain.Workbench.Enums;

/// <summary>
/// Lifecycle states for a detection-content issue. Mirrors the SIEM Detection Content
/// Issue workflow state machine. Terminal states: <see cref="Rejected"/> and
/// <see cref="Closed"/>.
/// </summary>
public enum IssueStatus
{
    /// <summary>Newly created; awaiting completeness check before triage.</summary>
    New = 0,

    /// <summary>Submitter asked for additional context before triage can proceed.</summary>
    NeedsInfo = 1,

    /// <summary>Reviewed and accepted into the backlog pipeline.</summary>
    Triaged = 2,

    /// <summary>Sensitive evidence detected; issue held pending sanitisation.</summary>
    SanitizationRequired = 3,

    /// <summary>Closed as duplicate, out of scope, invalid, or unsupported.</summary>
    Rejected = 4,

    /// <summary>Accepted into backlog; acceptance criteria not yet defined.</summary>
    Backlog = 5,

    /// <summary>Acceptance criteria defined; ready to be picked up.</summary>
    Ready = 6,

    /// <summary>Blocked on missing data, policy decision, or external dependency.</summary>
    Blocked = 7,

    /// <summary>Owner has started implementation work (Change opened).</summary>
    InProgress = 8,

    /// <summary>Implementation Change is under review.</summary>
    InReview = 9,

    /// <summary>Implementation Change has been accepted (merged to Git).</summary>
    Merged = 10,

    /// <summary>Detection library has been synchronised with the accepted change.</summary>
    Published = 11,

    /// <summary>Issue fully closed after publication or abandoned.</summary>
    Closed = 12,
}