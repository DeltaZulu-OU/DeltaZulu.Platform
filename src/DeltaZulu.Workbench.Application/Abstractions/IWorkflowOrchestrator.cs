using DeltaZulu.Workbench.Domain.Enums;
using DeltaZulu.Workbench.Domain.Identifiers;

namespace DeltaZulu.Workbench.Application.Abstractions;

/// <summary>
/// Internal workflow orchestration abstraction per ADR-0005 and ADR-0016. Domain and UI
/// layers do not reference Elsa types; they call this interface.
/// Two implementations exist:
/// <list type="bullet">
///   <item><description><c>DomainDrivenOrchestrator</c> — default; no Elsa dependency.</description></item>
///   <item><description><c>ElsaWorkflowOrchestrator</c> — wraps Elsa 3.x.</description></item>
/// </list>
/// </summary>
public interface IWorkflowOrchestrator
{
    Task OnChangeOpenedAsync(ChangeRequestId changeId, WorkflowProfileId profileId, CancellationToken ct = default);
    Task OnContentEditedAsync(ChangeRequestId changeId, CancellationToken ct = default);
    Task OnChecksCompletedAsync(ChangeRequestId changeId, CancellationToken ct = default);
    Task OnReviewRecordedAsync(ChangeRequestId changeId, ReviewDecision decision, CancellationToken ct = default);
    Task OnMergeCompletedAsync(ChangeRequestId changeId, CancellationToken ct = default);
    Task OnChangePublishedAsync(ChangeRequestId changeId, CancellationToken ct = default);
    Task OnChangeClosedAsync(ChangeRequestId changeId, CancellationToken ct = default);
}
