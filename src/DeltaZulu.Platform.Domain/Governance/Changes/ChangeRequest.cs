using DeltaZulu.Platform.Domain.Governance.Common;
using DeltaZulu.Platform.Domain.Governance.Enums;
using DeltaZulu.Platform.Domain.Governance.Identifiers;
using DeltaZulu.Platform.Domain.Governance.Reviews;
using DeltaZulu.Platform.Domain.Governance.Workflow;

namespace DeltaZulu.Platform.Domain.Governance.Changes;

/// <summary>
/// PR-like change against a detection. Carries proposed content, workflow profile, checks,
/// reviews, and merge state. Enforces self-approval block, approval reset on edit, stale
/// merge block, and required-check gating.
/// </summary>
public sealed class ChangeRequest : Entity<ChangeRequestId>
{
    private readonly List<ChangeDraftFile> _draftFiles = [];
    private readonly List<CheckRun> _checks = [];
    private readonly List<Review> _reviews = [];

    public string Key { get; }
    public string Title { get; }
    public DetectionId DetectionId { get; }
    public UserId AuthorId { get; }
    public WorkflowProfileId WorkflowProfileId { get; private set; }
    public VersionId? BaseVersionId { get; }
    public ChangeStatus Status { get; private set; }
    public bool IsStale { get; private set; }
    public string? StaleReason { get; private set; }
    public IssueId? LinkedIssueId { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? MergedAt { get; private set; }
    public VersionId? ResultVersionId { get; private set; }
    public string? CloseReason { get; private set; }

    public IReadOnlyList<ChangeDraftFile> DraftFiles => _draftFiles;
    public IReadOnlyList<CheckRun> Checks => _checks;
    public IReadOnlyList<Review> Reviews => _reviews;

    public IEnumerable<Review> EffectiveApprovals =>
        _reviews.Where(r => !r.IsSuperseded && r.Decision == ReviewDecision.Approved);

    public WorkflowProfile ResolveWorkflowProfile() => WorkflowProfile.For(WorkflowProfileId);

    private ChangeRequest(
        ChangeRequestId id, string key, string title, DetectionId detectionId,
        UserId authorId, WorkflowProfileId workflowProfileId, VersionId? baseVersionId,
        IssueId? linkedIssueId, DateTimeOffset createdAt)
        : base(id)
    {
        Key = key;
        Title = title;
        DetectionId = detectionId;
        AuthorId = authorId;
        WorkflowProfileId = workflowProfileId;
        BaseVersionId = baseVersionId;
        LinkedIssueId = linkedIssueId;
        Status = ChangeStatus.Draft;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static ChangeRequest Open(
        ChangeRequestId id, string key, string title, DetectionId detectionId,
        UserId authorId, WorkflowProfileId workflowProfileId, VersionId? baseVersionId,
        DateTimeOffset now, IssueId? linkedIssueId = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainException("change.key_empty", "Change key must not be empty.");
        if (key.Length > 32)
            throw new DomainException("change.key_too_long", "Change key exceeds 32 characters.");
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("change.title_empty", "Change title must not be empty.");
        if (title.Length > 200)
            throw new DomainException("change.title_too_long", "Change title exceeds 200 characters.");

        _ = WorkflowProfile.For(workflowProfileId);

        return new ChangeRequest(id, key, title, detectionId, authorId, workflowProfileId,
            baseVersionId, linkedIssueId, now);
    }

    /// <summary>Reconstitutes from persistence with pre-loaded sub-entities.</summary>
    public static ChangeRequest Reconstitute(
        ChangeRequestId id, string key, string title, DetectionId detectionId,
        UserId authorId, WorkflowProfileId workflowProfileId, VersionId? baseVersionId,
        ChangeStatus status, bool isStale, string? staleReason, IssueId? linkedIssueId,
        DateTimeOffset createdAt, DateTimeOffset updatedAt, DateTimeOffset? mergedAt,
        VersionId? resultVersionId, string? closeReason,
        IEnumerable<ChangeDraftFile> draftFiles, IEnumerable<CheckRun> checks,
        IEnumerable<Review> reviews)
    {
        var c = new ChangeRequest(id, key, title, detectionId, authorId, workflowProfileId,
            baseVersionId, linkedIssueId, createdAt)
        {
            Status = status,
            IsStale = isStale,
            StaleReason = staleReason,
            UpdatedAt = updatedAt,
            MergedAt = mergedAt,
            ResultVersionId = resultVersionId,
            CloseReason = closeReason
        };
        c._draftFiles.AddRange(draftFiles);
        c._checks.AddRange(checks);
        c._reviews.AddRange(reviews);
        return c;
    }

    public void SelectWorkflowProfile(WorkflowProfileId next, DateTimeOffset now)
    {
        EnsureMutable();
        if (_reviews.Count > 0)
        {
            throw new DomainException("change.profile_locked",
                "Workflow profile cannot be changed after a review has been recorded.");
        }

        _ = WorkflowProfile.For(next);
        WorkflowProfileId = next;
        UpdatedAt = now;
    }

    public void LinkIssue(IssueId issueId, DateTimeOffset now)
    {
        EnsureMutable();
        LinkedIssueId = issueId;
        UpdatedAt = now;
    }

    // --- draft content ----------------------------------------------------------------------

    public ChangeDraftFile UpsertDraftFile(
        LogicalPath path, DraftContentType contentType, string content,
        UserId editor, DateTimeOffset now)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(content);

        var existing = _draftFiles.FirstOrDefault(f => f.Path.Equals(path));
        ChangeDraftFile result;

        if (existing is null)
        {
            result = new ChangeDraftFile(Id, path, contentType, content, editor, now);
            _draftFiles.Add(result);
        }
        else
        {
            existing.Replace(content, contentType, editor, now);
            result = existing;
        }

        ApplyContentEditSideEffects(now);
        UpdatedAt = now;
        return result;
    }

    public void RemoveDraftFile(LogicalPath path, DateTimeOffset now)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(path);

        var existing = _draftFiles.FirstOrDefault(f => f.Path.Equals(path))
            ?? throw new DomainException("draft_file.not_found",
                $"Draft file '{path}' does not exist on this change.");

        _draftFiles.Remove(existing);
        ApplyContentEditSideEffects(now);
        UpdatedAt = now;
    }

    private void ApplyContentEditSideEffects(DateTimeOffset now)
    {
        var profile = ResolveWorkflowProfile();
        if (profile.ResetsApprovalOnContentEdit)
        {
            foreach (var review in _reviews.Where(r => !r.IsSuperseded && r.Decision == ReviewDecision.Approved))
                review.Supersede(now);
        }
        if (Status is ChangeStatus.ReviewRequired or ChangeStatus.ReadyToAccept)
            Status = ChangeStatus.Draft;
    }

    // --- checks -----------------------------------------------------------------------------

    public void ClearChecksForNewRun(DateTimeOffset now)
    {
        EnsureMutable();
        _checks.Clear();
        UpdatedAt = now;
    }

    public CheckRun QueueCheck(CheckRunId id, string name, bool isBlocking, DateTimeOffset now)
    {
        EnsureMutable();
        var run = new CheckRun(id, Id, name, isBlocking);
        _checks.Add(run);
        if (Status == ChangeStatus.Draft || Status == ChangeStatus.ChangesRequested)
            Status = ChangeStatus.ChecksRunning;
        UpdatedAt = now;
        return run;
    }

    public void AfterCheckPipelineCompleted(DateTimeOffset now)
    {
        EnsureMutable();
        if (_checks.Any(c => !c.IsTerminal))
        {
            UpdatedAt = now;
            return;
        }

        var readiness = EvaluateMergeReadiness();
        var profile = ResolveWorkflowProfile();

        var hasUnmetCheckGate = readiness.UnmetGates.Any(g =>
            g.Code is "gate.no_checks" || g.Code.StartsWith("gate.checks", StringComparison.Ordinal));

        if (profile.RequiresPassingChecks && hasUnmetCheckGate)
            Status = ChangeStatus.Draft;
        else if (profile.RequiresApproval)
            Status = ChangeStatus.ReviewRequired;
        else
            Status = ChangeStatus.ReadyToAccept;

        UpdatedAt = now;
    }

    // --- reviews ----------------------------------------------------------------------------

    public Review RecordReview(ReviewId id, UserId reviewerId, ReviewDecision decision, string comment, DateTimeOffset now)
    {
        EnsureMutable();
        var profile = ResolveWorkflowProfile();

        if (decision == ReviewDecision.Approved
            && profile.RequiresNonAuthorApprover
            && reviewerId.Equals(AuthorId))
        {
            throw new DomainException("review.self_approval_forbidden",
                $"Workflow profile '{profile.Id}' requires a non-author approver.");
        }

        var review = new Review(id, Id, reviewerId, decision, comment, now);
        _reviews.Add(review);

        if (decision == ReviewDecision.ChangesRequested)
        {
            Status = ChangeStatus.ChangesRequested;
        }
        else if (decision == ReviewDecision.Approved)
        {
            if (EvaluateMergeReadiness().IsReady)
                Status = ChangeStatus.ReadyToAccept;
            else if (Status == ChangeStatus.Draft || Status == ChangeStatus.ChangesRequested)
                Status = ChangeStatus.ReviewRequired;
        }

        UpdatedAt = now;
        return review;
    }

    // --- staleness --------------------------------------------------------------------------

    public void MarkStale(string reason, DateTimeOffset now)
    {
        EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        IsStale = true;
        StaleReason = reason;
        UpdatedAt = now;
    }

    // --- merge readiness and merge ----------------------------------------------------------

    public MergeReadiness EvaluateMergeReadiness()
    {
        var profile = ResolveWorkflowProfile();
        var unmet = new List<UnmetGate>();

        if (Status is ChangeStatus.Merged or ChangeStatus.Closed)
        {
            unmet.Add(new UnmetGate("gate.terminal_state",
                $"Change is in terminal state {Status} and cannot be re-merged."));
        }

        if (profile.BlocksStaleMerge && IsStale)
        {
            unmet.Add(new UnmetGate("gate.stale",
                "Detection changed after this change was opened. Review the latest version before accepting."));
        }

        if (profile.RequiresPassingChecks)
        {
            var blocking = _checks.Where(c => c.IsBlocking).ToList();
            var requiredNames = profile.RequiredBlockingCheckNames;

            if (blocking.Count == 0)
            {
                unmet.Add(new UnmetGate("gate.no_checks",
                    "Workflow profile requires passing checks, but no blocking checks have been run."));
            }
            else if (requiredNames.Count > 0)
            {
                var missingRequired = requiredNames
                    .Where(name => !blocking.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (missingRequired.Count > 0)
                {
                    unmet.Add(new UnmetGate("gate.checks_missing",
                        $"Required checks have not run: {string.Join(", ", missingRequired)}."));
                }

                var incompleteRequired = blocking
                    .Where(c => requiredNames.Contains(c.Name) && c.Status != CheckStatus.Passed)
                    .Select(c => c.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (incompleteRequired.Count > 0)
                {
                    unmet.Add(new UnmetGate("gate.checks_not_passed",
                        $"Required checks have not passed: {string.Join(", ", incompleteRequired)}."));
                }
            }
            else if (blocking.Any(c => c.Status != CheckStatus.Passed))
            {
                unmet.Add(new UnmetGate("gate.checks_not_passed",
                    "One or more required checks have not passed."));
            }
        }

        if (profile.RequiresApproval)
        {
            var effective = EffectiveApprovals.ToList();
            if (effective.Count == 0)
                unmet.Add(new UnmetGate("gate.approval_missing",
                    "Workflow profile requires an approval; none is recorded."));
            else if (profile.RequiresNonAuthorApprover && effective.All(r => r.ReviewerId.Equals(AuthorId)))
                unmet.Add(new UnmetGate("gate.non_author_approval_missing",
                    "Workflow profile requires approval by someone other than the change author."));
        }

        return unmet.Count == 0 ? MergeReadiness.Ready() : MergeReadiness.Blocked(unmet);
    }

    public void MarkMerged(VersionId resultVersionId, DateTimeOffset now)
    {
        EnsureMutable();
        var readiness = EvaluateMergeReadiness();
        if (!readiness.IsReady)
        {
            var first = readiness.UnmetGates[0];
            throw new DomainException(first.Code, first.Message);
        }
        Status = ChangeStatus.Merged;
        ResultVersionId = resultVersionId;
        MergedAt = now;
        UpdatedAt = now;
    }

    public void MarkPublished(DateTimeOffset now)
    {
        if (Status != ChangeStatus.Merged)
        {
            throw new DomainException("change.publish_requires_merged",
                "Only a merged change can be marked as published.");
        }

        Status = ChangeStatus.Published;
        UpdatedAt = now;
    }

    public void Close(string reason, DateTimeOffset now)
    {
        if (Status is ChangeStatus.Merged or ChangeStatus.Published)
            throw new DomainException("change.close_after_merge", "A merged change cannot be closed.");
        if (Status == ChangeStatus.Closed) return;
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Status = ChangeStatus.Closed;
        CloseReason = reason;
        UpdatedAt = now;
    }

    private void EnsureMutable()
    {
        if (Status is ChangeStatus.Merged or ChangeStatus.Published or ChangeStatus.Closed)
        {
            throw new DomainException("change.immutable",
                $"Change is in terminal state {Status} and cannot be modified.");
        }
    }
}