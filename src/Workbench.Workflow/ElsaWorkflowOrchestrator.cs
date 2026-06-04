using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Messages;
using Elsa.Workflows.Runtime.Options;
using Elsa.Workflows.Models;
using Microsoft.Extensions.Logging;
using Workbench.Application.Abstractions;
using Workbench.Domain.Enums;
using Workbench.Domain.Identifiers;
using Workbench.Workflow.Workflows;
using Elsa.Common.Models;

namespace Workbench.Workflow;

/// <summary>
/// <see cref="IWorkflowOrchestrator"/> backed by Elsa 3.7. Uses the new client API
/// (<c>CreateClientAsync</c>) for starting workflows and the (obsolete but functional)
/// <c>TriggerWorkflowsAsync</c> for dispatching lifecycle events to suspended instances.
/// </summary>
/// <remarks>
/// The domain aggregate remains the authoritative state owner. If the Elsa workflow gets out
/// of sync, the domain state is canonical. Elsa failures are logged and swallowed.
/// </remarks>
public sealed class ElsaWorkflowOrchestrator(
    IWorkflowRuntime runtime,
    ILogger<ElsaWorkflowOrchestrator> logger) : IWorkflowOrchestrator
{
    private static string CorrelationId(ChangeRequestId id) => $"change:{id}";

    public async Task OnChangeOpenedAsync(ChangeRequestId changeId, WorkflowProfileId profileId, CancellationToken ct = default)
    {
        logger.LogInformation("Elsa: starting workflow for change {ChangeId}, profile {Profile}.", changeId, profileId);
        try
        {
            var client = await runtime.CreateClientAsync(ct);
            await client.CreateAndRunInstanceAsync(new CreateAndRunWorkflowInstanceRequest
            {
                WorkflowDefinitionHandle = WorkflowDefinitionHandle.ByDefinitionId(
                    nameof(ChangeLifecycleWorkflow), VersionOptions.Published),
                CorrelationId = CorrelationId(changeId),
                Input = new Dictionary<string, object>
                {
                    ["ChangeId"] = changeId.ToString(),
                    ["WorkflowProfile"] = profileId.ToString(),
                },
            }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Elsa: failed to start workflow for change {ChangeId}.", changeId);
        }
    }

    public Task OnContentEditedAsync(ChangeRequestId changeId, CancellationToken ct = default)
        => DispatchEventAsync(changeId, ChangeLifecycleWorkflow.EventContentEdited, ct);

    public Task OnChecksCompletedAsync(ChangeRequestId changeId, CancellationToken ct = default)
        => DispatchEventAsync(changeId, ChangeLifecycleWorkflow.EventChecksCompleted, ct);

    public Task OnReviewRecordedAsync(ChangeRequestId changeId, ReviewDecision decision, CancellationToken ct = default)
        => DispatchEventAsync(changeId, ChangeLifecycleWorkflow.EventReviewRecorded, ct,
            new Dictionary<string, object> { ["Decision"] = decision.ToString() });

    public Task OnMergeCompletedAsync(ChangeRequestId changeId, CancellationToken ct = default)
        => DispatchEventAsync(changeId, ChangeLifecycleWorkflow.EventMergeCompleted, ct);

    public Task OnChangeClosedAsync(ChangeRequestId changeId, CancellationToken ct = default)
        => DispatchEventAsync(changeId, ChangeLifecycleWorkflow.EventClosed, ct);

    private async Task DispatchEventAsync(
        ChangeRequestId changeId,
        string eventName,
        CancellationToken ct,
        IDictionary<string, object>? input = null)
    {
        logger.LogInformation("Elsa: dispatching event {Event} for change {ChangeId}.", eventName, changeId);
        try
        {
            // The Event activity creates a bookmark with EventStimulus(eventName).
            // TriggerWorkflowsAsync matches bookmarks by activity type name + payload hash.
            // This API is obsolete in Elsa 3.7 but remains functional.
#pragma warning disable CS0618
            await runtime.TriggerWorkflowsAsync(
                activityTypeName: "Elsa.Event",
                bookmarkPayload: new { EventName = eventName },
                options: new TriggerWorkflowsOptions
                {
                    CorrelationId = CorrelationId(changeId),
                    Input = input ?? new Dictionary<string, object>(),
                });
#pragma warning restore CS0618
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Elsa: failed to dispatch event {Event} for change {ChangeId}.", eventName, changeId);
        }
    }
}
