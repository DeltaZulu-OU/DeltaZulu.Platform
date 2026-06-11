using DeltaZulu.Platform.Domain.Governance.Common;
using DeltaZulu.Platform.Domain.Governance.Contracts;
using DeltaZulu.Platform.Domain.Governance.Detections;
using DeltaZulu.Platform.Domain.Governance.Enums;
using DeltaZulu.Platform.Domain.Governance.Identifiers;

namespace DeltaZulu.Platform.Application.Governance.Services;

/// <summary>
/// Handles merge recovery markers created around accepted-content commits. It can list
/// unresolved merge attempts and repair committed accepted content whose database version
/// projection did not complete.
/// </summary>
public sealed class MergeReconciliationService(
    IMergeIntentRepository intents,
    IChangeRequestRepository changes,
    IDetectionRepository detections,
    IDetectionVersionRepository versions,
    IAcceptedContentStore contentStore,
    IUnitOfWork uow,
    TimeProvider time)
{
    /// <summary>
    /// Lists merge attempts that have not reached a completed database version projection.
    /// </summary>
    public Task<IReadOnlyList<MergeIntent>> ListUnresolvedAsync(CancellationToken ct = default)
        => intents.ListUnresolvedAsync(ct);

    /// <summary>
    /// Produces domain-language operator guidance for an unresolved merge intent without
    /// exposing low-level accepted-content storage commands in the UI.
    /// </summary>
    public async Task<MergeIntentRecoveryGuidance> GetRecoveryGuidanceAsync(
        ChangeRequestId changeId,
        CancellationToken ct = default)
    {
        var unresolved = await intents.ListUnresolvedAsync(ct);
        var intent = unresolved.FirstOrDefault(i => i.ChangeId.Equals(changeId));

        return intent is null
            ? MergeIntentRecoveryGuidance.NotFound(changeId)
            : BuildRecoveryGuidance(intent);
    }

    public static MergeIntentRecoveryGuidance BuildRecoveryGuidance(MergeIntent intent)
    {
        if (intent.State == MergeIntentState.Committed && !string.IsNullOrWhiteSpace(intent.CommitSha))
        {
            return MergeIntentRecoveryGuidance.Repairable(
                intent.ChangeId,
                "Accepted content was written, but the user-facing version projection did not finish. Use Repair projection to create the missing version and mark sibling changes stale.",
                "Repair projection");
        }

        return intent.State == MergeIntentState.Committed
            ? MergeIntentRecoveryGuidance.NeedsInvestigation(
                intent.ChangeId,
                "The recovery marker says accepted content was written, but no accepted snapshot is recorded. Verify the accepted-content store before retrying the merge or closing the marker.",
                "Verify accepted snapshot")
            : MergeIntentRecoveryGuidance.WaitingForAcceptedWrite(
            intent.ChangeId,
            "The accepted-content write has not completed. If this remains after the merge attempt stops running, retry the merge from the change page or investigate the accepted-content store health.",
            "Wait or retry merge");
    }

    /// <summary>
    /// Repairs one committed-but-unprojected merge intent by creating the missing domain
    /// version projection and moving the associated change/detection to their accepted state.
    /// Pending intents are reported but not modified because no accepted-content commit is
    /// available to project.
    /// </summary>
    public async Task<MergeRepairResult> RepairCommittedAsync(ChangeRequestId changeId, CancellationToken ct = default)
    {
        var unresolved = await intents.ListUnresolvedAsync(ct);
        var intent = unresolved.FirstOrDefault(i => i.ChangeId.Equals(changeId));

        if (intent is null)
            return MergeRepairResult.NotFound(changeId);

        if (intent.State != MergeIntentState.Committed || string.IsNullOrWhiteSpace(intent.CommitSha))
        {
            return MergeRepairResult.NotRepairable(changeId, intent.State,
                "Merge intent has not recorded an accepted-content commit yet.");
        }

        if (!await contentStore.CommitExistsAsync(intent.CommitSha, ct))
        {
            return MergeRepairResult.NotRepairable(changeId, intent.State,
                "Accepted-content commit was not found in the content store.");
        }

        var change = await changes.GetByIdAsync(changeId, ct)
            ?? throw new DomainException("change.not_found", $"Change '{changeId}' not found for merge repair.");

        var detection = await detections.GetByIdAsync(intent.DetectionId, ct)
            ?? throw new DomainException("detection.not_found",
                $"Detection '{intent.DetectionId}' not found for merge repair.");

        if (change.ResultVersionId is not null)
        {
            await intents.MarkCompletedAsync(changeId, change.ResultVersionId.Value, time.GetUtcNow(), ct);
            return MergeRepairResult.AlreadyProjected(changeId, change.ResultVersionId.Value);
        }

        var now = time.GetUtcNow();
        var versionId = VersionId.New();
        var version = DetectionVersion.Project(
            versionId,
            detection.Id,
            await versions.GetNextSequenceNumberAsync(detection.Id, ct),
            detection.Title,
            change.Title,
            change.AuthorId,
            change.WorkflowProfileId,
            change.Id,
            change.LinkedIssueId,
            intent.CommittedAt ?? now,
            change.DraftFiles.Select(f => f.Path).ToList(),
            intent.CommitSha,
            BuildChecksSummary(change),
            BuildReviewSummary(change));

        uow.BeginTransaction();

        versions.Add(version);
        change.MarkMerged(version.Id, now);
        detection.MarkAccepted(version.Id, now);
        await intents.MarkCompletedAsync(change.Id, version.Id, now, ct);

        changes.Save(change);
        detections.Save(detection);

        var siblings = await changes.ListByDetectionAsync(detection.Id, ct);
        foreach (var sibling in siblings)
        {
            if (sibling.Id.Equals(change.Id)) continue;
            if (sibling.Status is ChangeStatus.Merged or ChangeStatus.Closed) continue;

            sibling.MarkStale($"Change {change.Key} was repaired at version {version.DisplayVersion}.", now);
            changes.Save(sibling);
        }

        await uow.SaveChangesAsync(ct);

        return MergeRepairResult.Repaired(changeId, version.Id, intent.CommitSha);
    }

    private static string BuildChecksSummary(Domain.Governance.Changes.ChangeRequest change)
    {
        if (change.Checks.Count == 0) return "No checks.";
        var passed = change.Checks.Count(c => c.Status == CheckStatus.Passed);
        var failed = change.Checks.Count(c => c.Status == CheckStatus.Failed);
        return $"{passed} passed, {failed} failed (of {change.Checks.Count} total).";
    }

    private static string BuildReviewSummary(Domain.Governance.Changes.ChangeRequest change)
    {
        if (change.Reviews.Count == 0) return "No reviews.";
        var effective = change.Reviews.Where(r => !r.IsSuperseded).ToList();
        var approved = effective.Count(r => r.Decision == Domain.Governance.Enums.ReviewDecision.Approved);
        return $"{approved} approved (of {effective.Count} effective reviews).";
    }
}

public enum MergeRecoveryActionKind
{
    WaitingForAcceptedWrite,
    RepairProjection,
    NeedsInvestigation,
    NotFound,
}

public sealed record MergeIntentRecoveryGuidance(
    ChangeRequestId ChangeId,
    MergeRecoveryActionKind ActionKind,
    string Message,
    string RecommendedAction)
{
    public bool CanRepair => ActionKind == MergeRecoveryActionKind.RepairProjection;

    public static MergeIntentRecoveryGuidance Repairable(
        ChangeRequestId changeId, string message, string recommendedAction)
        => new(changeId, MergeRecoveryActionKind.RepairProjection, message, recommendedAction);

    public static MergeIntentRecoveryGuidance WaitingForAcceptedWrite(
        ChangeRequestId changeId, string message, string recommendedAction)
        => new(changeId, MergeRecoveryActionKind.WaitingForAcceptedWrite, message, recommendedAction);

    public static MergeIntentRecoveryGuidance NeedsInvestigation(
        ChangeRequestId changeId, string message, string recommendedAction)
        => new(changeId, MergeRecoveryActionKind.NeedsInvestigation, message, recommendedAction);

    public static MergeIntentRecoveryGuidance NotFound(ChangeRequestId changeId)
        => new(
            changeId,
            MergeRecoveryActionKind.NotFound,
            "No unresolved merge intent was found for this change. Refresh the recovery list before taking further action.",
            "Refresh recovery list");
}

public enum MergeRepairStatus
{
    Repaired,
    AlreadyProjected,
    NotFound,
    NotRepairable,
}

public sealed record MergeRepairResult(
    ChangeRequestId ChangeId,
    MergeRepairStatus Status,
    VersionId? VersionId,
    string? CommitSha,
    string Message)
{
    public bool IsSuccess => Status is MergeRepairStatus.Repaired or MergeRepairStatus.AlreadyProjected;

    public static MergeRepairResult Repaired(ChangeRequestId changeId, VersionId versionId, string commitSha)
        => new(changeId, MergeRepairStatus.Repaired, versionId, commitSha,
            "Created the missing version projection for committed accepted content.");

    public static MergeRepairResult AlreadyProjected(ChangeRequestId changeId, VersionId versionId)
        => new(changeId, MergeRepairStatus.AlreadyProjected, versionId, null,
            "Merge intent already has an associated version projection.");

    public static MergeRepairResult NotFound(ChangeRequestId changeId)
        => new(changeId, MergeRepairStatus.NotFound, null, null,
            "No unresolved merge intent was found for the change.");

    public static MergeRepairResult NotRepairable(
        ChangeRequestId changeId, MergeIntentState state, string reason)
        => new(changeId, MergeRepairStatus.NotRepairable, null, null,
            $"Merge intent in state '{state}' cannot be repaired automatically. {reason}");
}