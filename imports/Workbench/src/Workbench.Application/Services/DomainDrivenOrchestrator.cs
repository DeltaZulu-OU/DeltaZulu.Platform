using Microsoft.Extensions.Logging;
using Workbench.Application.Abstractions;
using Workbench.Domain.Enums;
using Workbench.Domain.Identifiers;

namespace Workbench.Application.Services;

/// <summary>
/// Default <see cref="IWorkflowOrchestrator"/> implementation. No Elsa dependency.
/// </summary>
public sealed class DomainDrivenOrchestrator(ILogger<DomainDrivenOrchestrator> logger) : IWorkflowOrchestrator
{
    public Task OnChangeOpenedAsync(ChangeRequestId changeId, WorkflowProfileId profileId, CancellationToken ct = default)
    {
        logger.LogInformation("Workflow: change {ChangeId} opened under profile {Profile}.", changeId, profileId);
        return Task.CompletedTask;
    }

    public Task OnContentEditedAsync(ChangeRequestId changeId, CancellationToken ct = default)
    {
        logger.LogInformation("Workflow: change {ChangeId} content edited.", changeId);
        return Task.CompletedTask;
    }

    public Task OnChecksCompletedAsync(ChangeRequestId changeId, CancellationToken ct = default)
    {
        logger.LogInformation("Workflow: change {ChangeId} checks completed.", changeId);
        return Task.CompletedTask;
    }

    public Task OnReviewRecordedAsync(ChangeRequestId changeId, ReviewDecision decision, CancellationToken ct = default)
    {
        logger.LogInformation("Workflow: change {ChangeId} review recorded: {Decision}.", changeId, decision);
        return Task.CompletedTask;
    }

    public Task OnMergeCompletedAsync(ChangeRequestId changeId, CancellationToken ct = default)
    {
        logger.LogInformation("Workflow: change {ChangeId} merge completed.", changeId);
        return Task.CompletedTask;
    }

    public Task OnChangePublishedAsync(ChangeRequestId changeId, CancellationToken ct = default)
    {
        logger.LogInformation("Workflow: change {ChangeId} published.", changeId);
        return Task.CompletedTask;
    }

    public Task OnChangeClosedAsync(ChangeRequestId changeId, CancellationToken ct = default)
    {
        logger.LogInformation("Workflow: change {ChangeId} closed.", changeId);
        return Task.CompletedTask;
    }
}
