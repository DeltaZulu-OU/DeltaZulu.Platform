using Elsa.Common.Models;
using Elsa.Workflows.Models;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Messages;
using Elsa.Workflows.Runtime.Options;
using Microsoft.Extensions.Logging;
using Workbench.Application.Abstractions;
using Workbench.Domain.Enums;
using Workbench.Domain.Identifiers;
using Workbench.Workflow.Workflows;

namespace Workbench.Workflow;

/// <summary>
/// <see cref="IWorkflowOrchestrator"/> backed by Elsa 3.7. Dispatches Change lifecycle
/// events to <see cref="ChangeLifecycleWorkflow"/> via bookmark stimuli.
/// </summary>
/// <remarks>
/// Domain aggregates remain the authoritative state owners. Elsa failures are logged and
/// swallowed so a workflow engine outage does not block domain operations.
/// </remarks>
public sealed class ElsaWorkflowOrchestrator(
    IWorkflowRuntime runtime,
    ILogger<ElsaWorkflowOrchestrator> logger) : IWorkflowOrchestrator
{
    private static string ChangeCorrelationId(ChangeRequestId id) => $"change:{id}";

    public async Task OnChangeOpenedAsync(ChangeRequestId changeId, WorkflowProfileId profileId, CancellationToken ct = default)
    {
        logger.LogInformation("Elsa: starting change workflow for {ChangeId}, profile {Profile}.", changeId, profileId);
        try
        {
            var client = await runtime.CreateClientAsync(ct);
            await client.CreateAndRunInstanceAsync(new CreateAndRunWorkflowInstanceRequest
            {
                WorkflowDefinitionHandle = WorkflowDefinitionHandle.ByDefinitionId(
                    nameof(ChangeLifecycleWorkflow), VersionOptions.Published),
                CorrelationId = ChangeCorrelationId(changeId),
                Input = new Dictionary<string, object>
                {
                    ["ChangeId"] = changeId.ToString(),
                    ["WorkflowProfile"] = profileId.ToString(),
                },
            }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Elsa: failed to start change workflow for {ChangeId}.", changeId);
        }
    }

    public Task OnContentEditedAsync(ChangeRequestId changeId, CancellationToken ct = default)
        => DispatchAsync(changeId, ChangeLifecycleWorkflow.EventContentEdited, ct);

    public Task OnChecksCompletedAsync(ChangeRequestId changeId, CancellationToken ct = default)
        => DispatchAsync(changeId, ChangeLifecycleWorkflow.EventChecksCompleted, ct);

    public Task OnReviewRecordedAsync(ChangeRequestId changeId, ReviewDecision decision, CancellationToken ct = default)
        => DispatchAsync(changeId, ChangeLifecycleWorkflow.EventReviewRecorded, ct,
            new Dictionary<string, object> { ["Decision"] = decision.ToString() });

    public Task OnMergeCompletedAsync(ChangeRequestId changeId, CancellationToken ct = default)
        => DispatchAsync(changeId, ChangeLifecycleWorkflow.EventMergeCompleted, ct);

    public Task OnChangePublishedAsync(ChangeRequestId changeId, CancellationToken ct = default)
        => DispatchAsync(changeId, ChangeLifecycleWorkflow.EventPublished, ct);

    public Task OnChangeClosedAsync(ChangeRequestId changeId, CancellationToken ct = default)
        => DispatchAsync(changeId, ChangeLifecycleWorkflow.EventClosed, ct);

    private async Task DispatchAsync(
        ChangeRequestId changeId, string eventName, CancellationToken ct,
        IDictionary<string, object>? input = null)
    {
        logger.LogInformation("Elsa: dispatching {Event} for change {ChangeId}.", eventName, changeId);
        try
        {
#pragma warning disable CS0618
            await runtime.TriggerWorkflowsAsync(
                activityTypeName: "Elsa.Event",
                bookmarkPayload: new { EventName = eventName },
                options: new TriggerWorkflowsOptions
                {
                    CorrelationId = ChangeCorrelationId(changeId),
                    Input = input ?? new Dictionary<string, object>(),
                });
#pragma warning restore CS0618
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Elsa: failed to dispatch {Event} for change {ChangeId}.", eventName, changeId);
        }
    }
}
