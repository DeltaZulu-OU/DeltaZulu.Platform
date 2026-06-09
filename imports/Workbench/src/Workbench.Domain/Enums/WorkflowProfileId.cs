namespace Workbench.Domain.Enums;

/// <summary>
/// Identifier of a vendor-defined workflow profile. Workflow profiles are governance modes,
/// not user-authored workflows, per ADR-0004 and ADR-0008. The POC implements
/// <see cref="QuickLab"/> and <see cref="ControlledReview"/>; the others are listed so that
/// future profile addition does not require a code-wide enum change.
/// </summary>
public enum WorkflowProfileId
{
    /// <summary>Fast experimentation; no approval; checks optional/warning-only.</summary>
    QuickLab = 0,

    /// <summary>Single maintainer; required checks; no external approval.</summary>
    SoloValidated = 1,

    /// <summary>Small team; required checks; one approval.</summary>
    StandardReview = 2,

    /// <summary>SOC controlled; required checks; non-author approval; stale-change blocking.</summary>
    ControlledReview = 3,

    /// <summary>Urgent fix with minimum checks plus follow-up review.</summary>
    EmergencyFix = 4,
}
