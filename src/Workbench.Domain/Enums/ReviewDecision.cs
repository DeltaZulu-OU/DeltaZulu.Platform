namespace Workbench.Domain.Enums;

/// <summary>Decision recorded by a reviewer on a change request.</summary>
public enum ReviewDecision
{
    /// <summary>Reviewer left a non-blocking comment without a decision.</summary>
    Commented = 0,

    /// <summary>Reviewer approves the change as currently proposed.</summary>
    Approved = 1,

    /// <summary>Reviewer requires the author to make further edits.</summary>
    ChangesRequested = 2,
}