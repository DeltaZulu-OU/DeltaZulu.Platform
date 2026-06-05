using Workbench.Application.Abstractions;
using Workbench.Domain.Common;
using Workbench.Domain.Detections;
using Workbench.Domain.Enums;
using Workbench.Domain.Identifiers;

namespace Workbench.Application.Services;

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
            return MergeRepairResult.NotRepairable(changeId, intent.State,
                "Merge intent has not recorded an accepted-content commit yet.");

        if (!await contentStore.CommitExistsAsync(intent.CommitSha, ct))
            return MergeRepairResult.NotRepairable(changeId, intent.State,
                "Accepted-content commit was not found in the content store.");

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

    private static string BuildChecksSummary(Domain.Changes.ChangeRequest change)
    {
        if (change.Checks.Count == 0) return "No checks.";
        var passed = change.Checks.Count(c => c.Status == CheckStatus.Passed);
        var failed = change.Checks.Count(c => c.Status == CheckStatus.Failed);
        return $"{passed} passed, {failed} failed (of {change.Checks.Count} total).";
    }

    private static string BuildReviewSummary(Domain.Changes.ChangeRequest change)
    {
        if (change.Reviews.Count == 0) return "No reviews.";
        var effective = change.Reviews.Where(r => !r.IsSuperseded).ToList();
        var approved = effective.Count(r => r.Decision == Domain.Enums.ReviewDecision.Approved);
        return $"{approved} approved (of {effective.Count} effective reviews).";
    }
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
