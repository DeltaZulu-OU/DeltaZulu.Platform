using DeltaZulu.Workbench.Domain.Changes;
using DeltaZulu.Workbench.Domain.Enums;
using DeltaZulu.Workbench.Domain.Identifiers;

namespace DeltaZulu.Workbench.Application.Abstractions;

/// <summary>
/// Context passed to an <see cref="ICheck"/> implementation. Contains the draft file
/// set and enough metadata for the check to do its work without touching persistence
/// or the content store.
/// </summary>
public sealed record CheckContext
{
    /// <summary>The change request being checked.</summary>
    public ChangeRequestId ChangeRequestId { get; }

    /// <summary>Slug of the target detection.</summary>
    public string DetectionSlug { get; }

    /// <summary>Workflow profile in force for this change.</summary>
    public WorkflowProfileId WorkflowProfileId { get; }

    /// <summary>All draft files on the change, keyed by logical path.</summary>
    public IReadOnlyList<DraftFileSnapshot> DraftFiles { get; }

    public CheckContext(
        ChangeRequestId changeRequestId,
        string detectionSlug,
        WorkflowProfileId workflowProfileId,
        IReadOnlyList<DraftFileSnapshot> draftFiles)
    {
        ChangeRequestId = changeRequestId;
        DetectionSlug = detectionSlug;
        WorkflowProfileId = workflowProfileId;
        DraftFiles = draftFiles;
    }
}

/// <summary>
/// Immutable snapshot of a draft file for check evaluation. Decoupled from the domain
/// <see cref="ChangeDraftFile"/> to avoid passing mutable entities into stateless checks.
/// </summary>
public sealed record DraftFileSnapshot(
    string LogicalPath,
    DraftContentType ContentType,
    string Content);