using DeltaZulu.Platform.Domain.Workbench.Common;
using DeltaZulu.Platform.Domain.Workbench.Contracts;
using DeltaZulu.Platform.Domain.Workbench.Identifiers;
using Microsoft.Extensions.Logging;

namespace DeltaZulu.Platform.Application.Workbench.Services;

public sealed partial class CheckPipelineRunner(
    IChangeRequestRepository changes,
    IDetectionRepository detections,
    IEnumerable<ICheck> checks,
    IWorkflowOrchestrator orchestrator,
    TimeProvider time,
    ILogger<CheckPipelineRunner> logger)
{
    public async Task<IReadOnlyList<PipelineCheckResult>> RunAsync(
        ChangeRequestId changeId, CancellationToken ct = default)
    {
        var change = await changes.GetByIdAsync(changeId, ct)
            ?? throw new DomainException("change.not_found", $"Change '{changeId}' not found.");

        var detection = await detections.GetByIdAsync(change.DetectionId, ct)
            ?? throw new DomainException("detection.not_found", $"Detection '{change.DetectionId}' not found.");

        var drafts = change.DraftFiles
            .Select(f => new DraftFileSnapshot(f.Path.Value, f.ContentType, f.Content))
            .ToList();

        var context = new CheckContext(change.Id, detection.Slug, change.WorkflowProfileId, drafts);
        var presentTypes = new HashSet<Domain.Workbench.Enums.DraftContentType>(drafts.Select(d => d.ContentType));

        var now = time.GetUtcNow();
        var results = new List<PipelineCheckResult>();

        change.ClearChecksForNewRun(now);

        foreach (var check in checks)
        {
            if (!check.ApplicableContentTypes.Any(t => presentTypes.Contains(t)))
            {
                LogSkippingCheck(logger, check.Name);
                continue;
            }

            var run = change.QueueCheck(CheckRunId.New(), check.Name, check.IsBlocking, now);
            run.MarkRunning(now);

            CheckOutcome outcome;
            try
            {
                outcome = await check.RunAsync(context, ct);
            }
            catch (Exception ex)
            {
                LogCheckUnhandledException(logger, ex, check.Name);
                var exDetail = ex.ToString();
                outcome = CheckOutcome.Fail(
                    $"Internal error: {ex.GetType().Name}: {ex.Message}",
                    "{}",
                    exDetail[..Math.Min(exDetail.Length, 2000)]);
            }

            run.Complete(outcome.Status, outcome.Summary, outcome.DetailsJson, outcome.LogsExcerpt, now);
            results.Add(new PipelineCheckResult(check.Name, outcome));
        }

        change.AfterCheckPipelineCompleted(now);
        changes.Save(change);

        await orchestrator.OnChecksCompletedAsync(changeId, ct);

        return results;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Skipping check {CheckName}: no applicable content types.")]
    private static partial void LogSkippingCheck(ILogger logger, string checkName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error,
        Message = "Check {CheckName} threw an unhandled exception.")]
    private static partial void LogCheckUnhandledException(ILogger logger, Exception ex, string checkName);
}

public sealed record PipelineCheckResult(string CheckName, CheckOutcome Outcome);