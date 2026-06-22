using DeltaZulu.Platform.Domain.Governance.Changes;
using DeltaZulu.Platform.Domain.Governance.Common;
using DeltaZulu.Platform.Domain.Governance.Contracts;
using DeltaZulu.Platform.Domain.Governance.Enums;
using DeltaZulu.Platform.Domain.Governance.Identifiers;
using System.Text;

namespace DeltaZulu.Platform.Application.Governance.Services;

public sealed class ChangeService(
    IChangeRequestRepository changes,
    IDetectionRepository detections,
    IWorkflowOrchestrator orchestrator,
    TimeProvider time)
{
    public async Task<ChangeRequest> OpenChangeAsync(
        string key, string title, DetectionId detectionId, UserId authorId,
        WorkflowProfileId workflowProfileId, IssueId? linkedIssueId = null,
        CancellationToken ct = default)
    {
        var detection = await detections.GetByIdAsync(detectionId, ct)
            ?? throw new DomainException("detection.not_found", $"Detection '{detectionId}' not found.");

        var change = ChangeRequest.Open(ChangeRequestId.New(), key, title, detectionId, authorId,
            workflowProfileId, detection.CurrentVersionId, time.GetUtcNow(), linkedIssueId);

        changes.Add(change);
        await orchestrator.OnChangeOpenedAsync(change.Id, workflowProfileId, ct);
        return change;
    }


    public Task<ChangeDraftFile> UpsertDetectionDraftAsync(
        ChangeRequestId changeId, DetectionDraftForm draft, UserId editor, CancellationToken ct = default)
        => UpsertDraftFileAsync(
            changeId,
            "detection.yaml",
            DraftContentType.DetectionMetadata,
            BuildDetectionYaml(draft),
            editor,
            ct);

    private static string BuildDetectionYaml(DetectionDraftForm draft)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"id: {YStr(draft.Id)}");
        sb.AppendLine($"title: {YStr(draft.Title)}");
        sb.AppendLine("description: >-");
        foreach (var line in (draft.Description ?? string.Empty).Split('\n'))
            sb.AppendLine($"  {line.TrimEnd()}");
        sb.AppendLine($"severity: {draft.Severity}");
        sb.AppendLine($"confidence: {draft.Confidence}");
        sb.AppendLine($"risk_score: {draft.RiskScore}");
        sb.AppendLine("content_type: detection");
        sb.AppendLine("query_language: kql");
        sb.AppendLine($"trigger_type: {draft.TriggerType}");

        if (draft.TriggerType == "nrt")
        {
            sb.AppendLine($"threshold: {draft.RealtimeThreshold ?? 1}");
        }
        else
        {
            sb.AppendLine($"schedule: '{draft.ScheduleExpression ?? "0 * * * *"}'");
            sb.AppendLine($"lookback: {draft.Lookback ?? "1h"}");
            sb.AppendLine($"max_alerts_per_run: {draft.MaxAlertsPerRun ?? 100}");
        }

        sb.AppendLine("query: |");
        var kql = (draft.KqlQuery ?? string.Empty).TrimEnd();
        if (string.IsNullOrWhiteSpace(kql))
        {
            sb.AppendLine("  # KQL query goes here");
        }
        else
        {
            foreach (var line in kql.Split('\n'))
                sb.AppendLine($"  {line.TrimEnd()}");
        }

        if (draft.MitreAttackTechniques.Count > 0)
        {
            sb.AppendLine("techniques:");
            foreach (var item in draft.MitreAttackTechniques)
                sb.AppendLine($"  - {item}");
        }

        if (draft.EntityMappings.Count > 0)
        {
            sb.AppendLine("entity_mappings:");
            foreach (var mapping in draft.EntityMappings)
            {
                sb.AppendLine($"  - type: {mapping.Type}");
                sb.AppendLine($"    field: {mapping.Field}");
            }
        }

        if (draft.FalsePositiveNotes.Count > 0)
        {
            sb.AppendLine("false_positive_notes:");
            foreach (var note in draft.FalsePositiveNotes)
                sb.AppendLine($"  - {note}");
        }

        return sb.ToString();
    }

    private static string YStr(string value)
    {
        var escaped = (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"\"{escaped}\"";
    }

    public async Task<ChangeDraftFile> UpsertDraftFileAsync(
        ChangeRequestId changeId, string logicalPath, DraftContentType contentType,
        string content, UserId editor, CancellationToken ct = default)
    {
        var change = await GetOrThrowAsync(changeId, ct);
        var path = LogicalPath.Parse(logicalPath);
        var file = change.UpsertDraftFile(path, contentType, content, editor, time.GetUtcNow());
        changes.Save(change);
        await orchestrator.OnContentEditedAsync(changeId, ct);
        return file;
    }

    public async Task RemoveDraftFileAsync(
        ChangeRequestId changeId, string logicalPath, CancellationToken ct = default)
    {
        var change = await GetOrThrowAsync(changeId, ct);
        change.RemoveDraftFile(LogicalPath.Parse(logicalPath), time.GetUtcNow());
        changes.Save(change);
        await orchestrator.OnContentEditedAsync(changeId, ct);
    }

    public async Task LinkIssueAsync(
        ChangeRequestId changeId, IssueId issueId, CancellationToken ct = default)
    {
        var change = await GetOrThrowAsync(changeId, ct);
        change.LinkIssue(issueId, time.GetUtcNow());
        changes.Save(change);
    }

    public async Task SelectWorkflowProfileAsync(
        ChangeRequestId changeId, WorkflowProfileId profileId, CancellationToken ct = default)
    {
        var change = await GetOrThrowAsync(changeId, ct);
        change.SelectWorkflowProfile(profileId, time.GetUtcNow());
        changes.Save(change);
    }

    public async Task<Domain.Governance.Reviews.Review> RecordReviewAsync(
        ChangeRequestId changeId, UserId reviewerId, ReviewDecision decision,
        string comment, CancellationToken ct = default)
    {
        var change = await GetOrThrowAsync(changeId, ct);
        var review = change.RecordReview(ReviewId.New(), reviewerId, decision, comment, time.GetUtcNow());
        changes.Save(change);
        await orchestrator.OnReviewRecordedAsync(changeId, decision, ct);
        return review;
    }

    public async Task CloseAsync(
        ChangeRequestId changeId, string reason, CancellationToken ct = default)
    {
        var change = await GetOrThrowAsync(changeId, ct);
        change.Close(reason, time.GetUtcNow());
        changes.Save(change);
        await orchestrator.OnChangeClosedAsync(changeId, ct);
    }

    public Task<ChangeRequest?> GetByIdAsync(ChangeRequestId id, CancellationToken ct = default)
        => changes.GetByIdAsync(id, ct);

    public Task<IReadOnlyList<ChangeRequest>> ListAsync(CancellationToken ct = default)
        => changes.ListAsync(ct);

    public Task<IReadOnlyList<ChangeRequest>> ListByDetectionAsync(DetectionId detectionId, CancellationToken ct = default)
        => changes.ListByDetectionAsync(detectionId, ct);

    public Task<IReadOnlyList<ChangeRequest>> ListMyActiveChangesAsync(UserId userId, CancellationToken ct = default)
        => changes.ListOpenByAuthorAsync(userId, ct);

    public Task<IReadOnlyList<ChangeRequest>> ListAwaitingMyReviewAsync(UserId userId, CancellationToken ct = default)
        => changes.ListAwaitingReviewAsync(userId, ct);

    public Task<IReadOnlyList<ChangeRequest>> ListWithFailedChecksAsync(CancellationToken ct = default)
        => changes.ListWithFailedBlockingChecksAsync(ct);

    private async Task<ChangeRequest> GetOrThrowAsync(ChangeRequestId id, CancellationToken ct)
        => await changes.GetByIdAsync(id, ct)
           ?? throw new DomainException("change.not_found", $"Change '{id}' not found.");
}