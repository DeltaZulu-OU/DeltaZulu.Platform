using DeltaZulu.Platform.Application.Workbench.Abstractions;
using DeltaZulu.Platform.Domain.Workbench.Enums;
using DeltaZulu.Platform.Domain.Workbench.Identifiers;
using DeltaZulu.Platform.Application.Workbench.Workflow.Workflows;
using Elsa.Common.Models;
using Elsa.Workflows.Models;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Messages;
using Elsa.Workflows.Runtime.Options;
using Microsoft.Extensions.Logging;

namespace DeltaZulu.Platform.Application.Workbench.Workflow;

/// <summary>
/// <see cref="IWorkflowOrchestrator"/> backed by Elsa 3.7. Dispatches Change lifecycle
/// events to <see cref="ChangeLifecycleWorkflow"/> via bookmark stimuli.
/// </summary>
/// <remarks>
/// Domain aggregates remain the authoritative state owners. Elsa failures are logged and
/// swallowed so a workflow engine outage does not block domain operations.
/// </remarks>
public sealed partial class ElsaWorkflowOrchestrator(
    IWorkflowRuntime runtime,
    ILogger<ElsaWorkflowOrchestrator> logger) : IWorkflowOrchestrator
{
    private static string ChangeCorrelationId(ChangeRequestId id) => $"change:{id}";

    public async Task OnChangeOpenedAsync(ChangeRequestId changeId, WorkflowProfileId profileId, CancellationToken ct = default)
    {
        LogStartingChangeWorkflow(logger, changeId, profileId);
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
            LogFailedToStartChangeWorkflow(logger, ex, changeId);
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
        LogDispatchingEvent(logger, eventName, changeId);
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
            LogFailedToDispatchEvent(logger, ex, eventName, changeId);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Elsa: starting change workflow for {ChangeId}, profile {Profile}.")]
    private static partial void LogStartingChangeWorkflow(ILogger logger, ChangeRequestId changeId, WorkflowProfileId profile);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Elsa: failed to start change workflow for {ChangeId}.")]
    private static partial void LogFailedToStartChangeWorkflow(ILogger logger, Exception exception, ChangeRequestId changeId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Elsa: dispatching {EventName} for change {ChangeId}.")]
    private static partial void LogDispatchingEvent(ILogger logger, string eventName, ChangeRequestId changeId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Elsa: failed to dispatch {EventName} for change {ChangeId}.")]
    private static partial void LogFailedToDispatchEvent(ILogger logger, Exception exception, string eventName, ChangeRequestId changeId);
}