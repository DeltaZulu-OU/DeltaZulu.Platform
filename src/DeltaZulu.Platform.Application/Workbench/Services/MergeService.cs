using DeltaZulu.Platform.Application.Workbench.ContentPipeline;
using DeltaZulu.Platform.Domain.Workbench.Changes;
using DeltaZulu.Platform.Domain.Workbench.Common;
using DeltaZulu.Platform.Domain.Workbench.Contracts;
using DeltaZulu.Platform.Domain.Workbench.Detections;
using DeltaZulu.Platform.Domain.Workbench.Identifiers;

namespace DeltaZulu.Platform.Application.Workbench.Services;

/// <summary>
/// Orchestrates the accept/merge flow: validates merge readiness, invokes the canonical
/// writer, commits to the accepted content store, creates a version projection, and updates
/// the detection aggregate.
/// </summary>
public sealed class MergeService(
    IChangeRequestRepository changes,
    IDetectionRepository detections,
    IDetectionVersionRepository versions,
    IAcceptedContentStore contentStore,
    IMergeIntentRepository mergeIntents,
    IUnitOfWork uow,
    IWorkflowOrchestrator orchestrator,
    TimeProvider time)
{
    public async Task<DetectionVersion> MergeAsync(
        ChangeRequestId changeId, string authorName, string authorEmail,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorEmail);

        var change = await changes.GetByIdAsync(changeId, ct)
            ?? throw new DomainException("change.not_found", $"Change '{changeId}' not found.");

        var detection = await detections.GetByIdAsync(change.DetectionId, ct)
            ?? throw new DomainException("detection.not_found", $"Detection '{change.DetectionId}' not found.");

        // Authoritative stale check: compare base version to current accepted version.
        var profile = change.ResolveWorkflowProfile();
        if (profile.BlocksStaleMerge && change.BaseVersionId != detection.CurrentVersionId)
        {
            change.MarkStale(
                "Base version does not match the current accepted version of the detection.", time.GetUtcNow());
            changes.Save(change);
            await uow.SaveChangesAsync(ct);
            throw new DomainException("gate.stale",
                "Detection changed after this change was opened. Review the latest version before accepting.");
        }

        // Gate check.
        var readiness = change.EvaluateMergeReadiness();
        if (!readiness.IsReady)
        {
            var first = readiness.UnmetGates[0];
            throw new DomainException(first.Code, first.Message);
        }

        // Build a patch-style commit: draft files add/update canonical content, while
        // absent accepted files are preserved unless a future domain command records an
        // explicit deletion. This prevents partial edits from deleting unrelated files.
        var commitRequest = CanonicalWriter.BuildCommitRequest(change, detection.Slug, authorName, authorEmail);

        await mergeIntents.CreatePendingAsync(new MergeIntent(
            change.Id,
            detection.Id,
            detection.Slug,
            time.GetUtcNow(),
            authorName,
            authorEmail,
            commitRequest.Message,
            MergeIntentState.Pending), ct);

        // Commit to Git, then immediately store the resulting commit SHA before the version
        // projection transaction. If the process fails after this point, the reconciliation
        // read model can surface committed accepted content that still needs DB repair.
        var commitResult = await contentStore.CommitAsync(commitRequest, ct);
        await mergeIntents.MarkCommittedAsync(change.Id, commitResult.CommitSha, commitResult.CommittedAt, ct);

        // Version projection.
        var now = time.GetUtcNow();
        var seqNum = await versions.GetNextSequenceNumberAsync(detection.Id, ct);
        var changedSections = change.DraftFiles.Select(f => f.Path).ToList();

        var version = DetectionVersion.Project(
            VersionId.New(), detection.Id, seqNum, detection.Title, change.Title,
            change.AuthorId, change.WorkflowProfileId, change.Id, change.LinkedIssueId,
            now, changedSections, commitResult.CommitSha,
            BuildChecksSummary(change), BuildReviewSummary(change));

        // Persist all DB changes atomically.
        uow.BeginTransaction();

        versions.Add(version);
        change.MarkMerged(version.Id, now);
        detection.MarkAccepted(version.Id, now);
        await mergeIntents.MarkCompletedAsync(change.Id, version.Id, now, ct);

        changes.Save(change);
        detections.Save(detection);

        // Mark sibling open changes as stale.
        var siblings = await changes.ListByDetectionAsync(detection.Id, ct);
        foreach (var sibling in siblings)
        {
            if (sibling.Id.Equals(change.Id)) continue;
            if (sibling.Status is Domain.Workbench.Enums.ChangeStatus.Merged or Domain.Workbench.Enums.ChangeStatus.Closed) continue;
            sibling.MarkStale($"Change {change.Key} merged at version {version.DisplayVersion}.", now);
            changes.Save(sibling);
        }

        await uow.SaveChangesAsync(ct);
        await orchestrator.OnMergeCompletedAsync(changeId, ct);

        return version;
    }

    private static string BuildChecksSummary(ChangeRequest change)
    {
        if (change.Checks.Count == 0) return "No checks.";
        var passed = change.Checks.Count(c => c.Status == Domain.Workbench.Enums.CheckStatus.Passed);
        var failed = change.Checks.Count(c => c.Status == Domain.Workbench.Enums.CheckStatus.Failed);
        return $"{passed} passed, {failed} failed (of {change.Checks.Count} total).";
    }

    private static string BuildReviewSummary(ChangeRequest change)
    {
        if (change.Reviews.Count == 0) return "No reviews.";
        var effective = change.Reviews.Where(r => !r.IsSuperseded).ToList();
        var approved = effective.Count(r => r.Decision == Domain.Workbench.Enums.ReviewDecision.Approved);
        return $"{approved} approved (of {effective.Count} effective reviews).";
    }
}