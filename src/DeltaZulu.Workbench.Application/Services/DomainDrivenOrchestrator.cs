using DeltaZulu.Workbench.Application.Abstractions;
using DeltaZulu.Workbench.Domain.Enums;
using DeltaZulu.Workbench.Domain.Identifiers;
using Microsoft.Extensions.Logging;

namespace DeltaZulu.Workbench.Application.Services;

/// <summary>
/// Default <see cref="IWorkflowOrchestrator"/> implementation. No Elsa dependency.
/// </summary>
public sealed partial class DomainDrivenOrchestrator(ILogger<DomainDrivenOrchestrator> logger) : IWorkflowOrchestrator
{
    public Task OnChangeOpenedAsync(ChangeRequestId changeId, WorkflowProfileId profileId, CancellationToken ct = default)
    {
        LogChangeOpened(logger, changeId, profileId);
        return Task.CompletedTask;
    }

    public Task OnContentEditedAsync(ChangeRequestId changeId, CancellationToken ct = default)
    {
        LogContentEdited(logger, changeId);
        return Task.CompletedTask;
    }

    public Task OnChecksCompletedAsync(ChangeRequestId changeId, CancellationToken ct = default)
    {
        LogChecksCompleted(logger, changeId);
        return Task.CompletedTask;
    }

    public Task OnReviewRecordedAsync(ChangeRequestId changeId, ReviewDecision decision, CancellationToken ct = default)
    {
        LogReviewRecorded(logger, changeId, decision);
        return Task.CompletedTask;
    }

    public Task OnMergeCompletedAsync(ChangeRequestId changeId, CancellationToken ct = default)
    {
        LogMergeCompleted(logger, changeId);
        return Task.CompletedTask;
    }

    public Task OnChangePublishedAsync(ChangeRequestId changeId, CancellationToken ct = default)
    {
        LogChangePublished(logger, changeId);
        return Task.CompletedTask;
    }

    public Task OnChangeClosedAsync(ChangeRequestId changeId, CancellationToken ct = default)
    {
        LogChangeClosed(logger, changeId);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Workflow: change {ChangeId} opened under profile {Profile}.")]
    private static partial void LogChangeOpened(ILogger logger, ChangeRequestId changeId, WorkflowProfileId profile);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Workflow: change {ChangeId} content edited.")]
    private static partial void LogContentEdited(ILogger logger, ChangeRequestId changeId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Workflow: change {ChangeId} checks completed.")]
    private static partial void LogChecksCompleted(ILogger logger, ChangeRequestId changeId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Workflow: change {ChangeId} review recorded: {Decision}.")]
    private static partial void LogReviewRecorded(ILogger logger, ChangeRequestId changeId, ReviewDecision decision);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Workflow: change {ChangeId} merge completed.")]
    private static partial void LogMergeCompleted(ILogger logger, ChangeRequestId changeId);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Workflow: change {ChangeId} published.")]
    private static partial void LogChangePublished(ILogger logger, ChangeRequestId changeId);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Workflow: change {ChangeId} closed.")]
    private static partial void LogChangeClosed(ILogger logger, ChangeRequestId changeId);
}