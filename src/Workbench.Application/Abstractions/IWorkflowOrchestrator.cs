using Workbench.Domain.Enums;
using Workbench.Domain.Identifiers;

namespace Workbench.Application.Abstractions;

/// <summary>
/// Internal workflow orchestration abstraction per ADR-0005. Domain and UI layers do not
/// reference Elsa types; they call this interface. Two implementations exist:
/// <list type="bullet">
///   <item><description><c>DomainDrivenOrchestrator</c> — default; delegates directly to
///   domain aggregate state-machine methods. No Elsa dependency.</description></item>
///   <item><description><c>ElsaWorkflowOrchestrator</c> — wraps Elsa 3.x; externalises
///   the lifecycle as a coded workflow for visual editing and timer-based
///   escalations.</description></item>
/// </list>
/// </summary>
public interface IWorkflowOrchestrator
{
    /// <summary>
    /// Signals that a new change request has been opened and its lifecycle should begin
    /// under the specified workflow profile.
    /// </summary>
    Task OnChangeOpenedAsync(ChangeRequestId changeId, WorkflowProfileId profileId, CancellationToken ct = default);

    /// <summary>Signals that draft content has been edited.</summary>
    Task OnContentEditedAsync(ChangeRequestId changeId, CancellationToken ct = default);

    /// <summary>Signals that the check pipeline has completed.</summary>
    Task OnChecksCompletedAsync(ChangeRequestId changeId, CancellationToken ct = default);

    /// <summary>Signals that a review has been recorded.</summary>
    Task OnReviewRecordedAsync(ChangeRequestId changeId, ReviewDecision decision, CancellationToken ct = default);

    /// <summary>Signals that a merge has completed successfully.</summary>
    Task OnMergeCompletedAsync(ChangeRequestId changeId, CancellationToken ct = default);

    /// <summary>Signals that the change has been closed without merge.</summary>
    Task OnChangeClosedAsync(ChangeRequestId changeId, CancellationToken ct = default);
}
