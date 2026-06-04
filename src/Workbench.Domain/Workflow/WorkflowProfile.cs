using Workbench.Domain.Enums;

namespace Workbench.Domain.Workflow;

/// <summary>
/// Vendor-defined governance policy bound to a <see cref="WorkflowProfileId"/>. Profiles are
/// not user-authored (ADR-0004) and cannot be mutated at runtime. The static
/// <see cref="For(WorkflowProfileId)"/> catalogue is the only source of profile data inside
/// the domain layer; the application layer may decorate this with display metadata.
/// </summary>
/// <remarks>
/// The gate semantics encoded here are the ones tested by the invariant suite. Adding fields
/// is acceptable; changing the existing semantics for <see cref="WorkflowProfileId.QuickLab"/>
/// or <see cref="WorkflowProfileId.ControlledReview"/> requires a new ADR.
/// </remarks>
public sealed class WorkflowProfile
{
    /// <summary>The identifier of this profile.</summary>
    public WorkflowProfileId Id { get; }

    /// <summary>
    /// When true, required checks must be in <c>Passed</c> state before the change can be merged.
    /// When false, failed required checks are warning-only.
    /// </summary>
    public bool RequiresPassingChecks { get; }

    /// <summary>
    /// When true, at least one <c>Approved</c> review is required before merge.
    /// </summary>
    public bool RequiresApproval { get; }

    /// <summary>
    /// When true, the approver must not be the change author. Implies <see cref="RequiresApproval"/>.
    /// </summary>
    public bool RequiresNonAuthorApprover { get; }

    /// <summary>
    /// When true, editing draft content after a recorded approval invalidates that approval and
    /// the change must be re-approved.
    /// </summary>
    public bool ResetsApprovalOnContentEdit { get; }

    /// <summary>
    /// When true, the merge service blocks merge if the base version of this change no longer
    /// matches the current accepted version of the target detection.
    /// </summary>
    public bool BlocksStaleMerge { get; }

    private WorkflowProfile(
        WorkflowProfileId id,
        bool requiresPassingChecks,
        bool requiresApproval,
        bool requiresNonAuthorApprover,
        bool resetsApprovalOnContentEdit,
        bool blocksStaleMerge)
    {
        if (requiresNonAuthorApprover && !requiresApproval)
        {
            throw new InvalidOperationException(
                "RequiresNonAuthorApprover implies RequiresApproval; profile catalogue is inconsistent.");
        }

        Id = id;
        RequiresPassingChecks = requiresPassingChecks;
        RequiresApproval = requiresApproval;
        RequiresNonAuthorApprover = requiresNonAuthorApprover;
        ResetsApprovalOnContentEdit = resetsApprovalOnContentEdit;
        BlocksStaleMerge = blocksStaleMerge;
    }

    /// <summary>
    /// Resolves the gate policy for a profile id. Throws for profiles that are reserved in the
    /// enum but not yet implemented in the POC.
    /// </summary>
    public static WorkflowProfile For(WorkflowProfileId id) => id switch
    {
        WorkflowProfileId.QuickLab => new WorkflowProfile(
            id: WorkflowProfileId.QuickLab,
            requiresPassingChecks: false,
            requiresApproval: false,
            requiresNonAuthorApprover: false,
            resetsApprovalOnContentEdit: false,
            blocksStaleMerge: false),

        WorkflowProfileId.ControlledReview => new WorkflowProfile(
            id: WorkflowProfileId.ControlledReview,
            requiresPassingChecks: true,
            requiresApproval: true,
            requiresNonAuthorApprover: true,
            resetsApprovalOnContentEdit: true,
            blocksStaleMerge: true),

        WorkflowProfileId.SoloValidated
            or WorkflowProfileId.StandardReview
            or WorkflowProfileId.EmergencyFix =>
            throw new NotSupportedException(
                $"Workflow profile '{id}' is reserved but not implemented in the POC. " +
                "Implement before exposing in the catalogue."),

        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown workflow profile."),
    };
}
