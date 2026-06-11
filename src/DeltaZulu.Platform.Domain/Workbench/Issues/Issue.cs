using DeltaZulu.Platform.Domain.Workbench.Common;
using DeltaZulu.Platform.Domain.Workbench.Enums;
using DeltaZulu.Platform.Domain.Workbench.Identifiers;

namespace DeltaZulu.Platform.Domain.Workbench.Issues;

/// <summary>
/// Detection-content issue aggregate. Implements the SIEM Detection Content Issue
/// lifecycle: New → Triaged → Backlog → Ready → InProgress → InReview → Merged →
/// Published → Closed (with NeedsInfo, SanitizationRequired, Blocked, and Rejected paths).
/// External case management (FlowIntel, TheHive) links via <see cref="ExternalCaseRef"/>.
/// </summary>
public sealed class Issue : Entity<IssueId>
{
    public string Key { get; }
    public string Title { get; private set; }
    public IssueType Type { get; }
    public IssueStatus Status { get; private set; }
    public ExternalCaseRef? ExternalCase { get; private set; }

    // Structured intake fields — optional; populated based on IssueType.
    public string? Description { get; private set; }

    public string? AcceptanceCriteria { get; private set; }
    public string? DataSource { get; private set; }
    public string? Platform { get; private set; }
    public string? AttackTechniqueId { get; private set; }

    // Classification
    public TlpLevel? Tlp { get; private set; }

    public IReadOnlyList<string> Labels { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private static readonly IReadOnlySet<IssueStatus> TerminalStatuses =
        new HashSet<IssueStatus> { IssueStatus.Rejected, IssueStatus.Closed };

    private Issue(IssueId id, string key, string title, IssueType type, DateTimeOffset createdAt)
        : base(id)
    {
        Key = key;
        Title = title;
        Type = type;
        Status = IssueStatus.New;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static Issue Create(IssueId id, string key, string title, IssueType type, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainException("issue.key_empty", "Issue key must not be empty.");
        if (key.Length > 32)
            throw new DomainException("issue.key_too_long", "Issue key exceeds 32 characters.");
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("issue.title_empty", "Issue title must not be empty.");
        return title.Length > 200
            ? throw new DomainException("issue.title_too_long", "Issue title exceeds 200 characters.")
            : new Issue(id, key, title, type, now);
    }

    public static Issue Reconstitute(
        IssueId id, string key, string title, IssueType type, IssueStatus status,
        ExternalCaseRef? externalCase, DateTimeOffset createdAt, DateTimeOffset updatedAt,
        string? description = null, string? acceptanceCriteria = null,
        string? dataSource = null, string? platform = null, string? attackTechniqueId = null,
        TlpLevel? tlp = null, IReadOnlyList<string>? labels = null)
    {
        return new Issue(id, key, title, type, createdAt)
        {
            Status = status,
            ExternalCase = externalCase,
            Description = description,
            AcceptanceCriteria = acceptanceCriteria,
            DataSource = dataSource,
            Platform = platform,
            AttackTechniqueId = attackTechniqueId,
            Tlp = tlp,
            Labels = labels ?? [],
            UpdatedAt = updatedAt
        };
    }

    // --- classification ---------------------------------------------------------------------

    public void Classify(TlpLevel? tlp, IReadOnlyList<string>? labels, DateTimeOffset now)
    {
        if (labels is not null)
        {
            if (labels.Count > 20)
                throw new DomainException("issue.too_many_labels", "An issue may not have more than 20 labels.");
            foreach (var label in labels)
            {
                if (string.IsNullOrWhiteSpace(label))
                    throw new DomainException("issue.label_empty", "Labels must not be empty.");
                if (label.Length > 64)
                    throw new DomainException("issue.label_too_long", $"Label '{label}' exceeds 64 characters.");
            }
        }
        Tlp = tlp;
        Labels = labels?.Select(l => l.Trim()).Where(l => l.Length > 0).ToList() ?? [];
        UpdatedAt = now;
    }

    // --- metadata ---------------------------------------------------------------------------

    public void Rename(string newTitle, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
            throw new DomainException("issue.title_empty", "Issue title must not be empty.");
        if (newTitle.Length > 200)
            throw new DomainException("issue.title_too_long", "Issue title exceeds 200 characters.");
        Title = newTitle;
        UpdatedAt = now;
    }

    public void UpdateIntakeFields(
        string? description, string? acceptanceCriteria,
        string? dataSource, string? platform, string? attackTechniqueId,
        DateTimeOffset now)
    {
        if (description?.Length > 2000)
            throw new DomainException("issue.description_too_long", "Description exceeds 2000 characters.");
        if (acceptanceCriteria?.Length > 2000)
            throw new DomainException("issue.criteria_too_long", "Acceptance criteria exceeds 2000 characters.");
        if (dataSource?.Length > 128)
            throw new DomainException("issue.data_source_too_long", "Data source exceeds 128 characters.");
        if (platform?.Length > 64)
            throw new DomainException("issue.platform_too_long", "Platform exceeds 64 characters.");
        if (attackTechniqueId?.Length > 32)
            throw new DomainException("issue.attack_technique_too_long", "ATT&CK technique ID exceeds 32 characters.");

        Description = description;
        AcceptanceCriteria = acceptanceCriteria;
        DataSource = dataSource;
        Platform = platform;
        AttackTechniqueId = attackTechniqueId;
        UpdatedAt = now;
    }

    // --- lifecycle transitions --------------------------------------------------------------

    public void RequestInfo(DateTimeOffset now)
    {
        RequireStatus("issue.request_info_invalid", IssueStatus.New, IssueStatus.Triaged);
        Status = IssueStatus.NeedsInfo;
        UpdatedAt = now;
    }

    public void ProvideInfo(DateTimeOffset now)
    {
        RequireStatus("issue.provide_info_invalid", IssueStatus.NeedsInfo);
        Status = IssueStatus.Triaged;
        UpdatedAt = now;
    }

    public void Triage(DateTimeOffset now)
    {
        RequireStatus("issue.triage_invalid", IssueStatus.New, IssueStatus.NeedsInfo);
        Status = IssueStatus.Triaged;
        UpdatedAt = now;
    }

    public void RequireSanitization(DateTimeOffset now)
    {
        RequireStatus("issue.sanitization_invalid", IssueStatus.Triaged);
        Status = IssueStatus.SanitizationRequired;
        UpdatedAt = now;
    }

    public void Sanitize(DateTimeOffset now)
    {
        RequireStatus("issue.sanitize_invalid", IssueStatus.SanitizationRequired);
        Status = IssueStatus.Triaged;
        UpdatedAt = now;
    }

    public void Accept(DateTimeOffset now)
    {
        RequireStatus("issue.accept_invalid", IssueStatus.Triaged);
        Status = IssueStatus.Backlog;
        UpdatedAt = now;
    }

    public void Reject(DateTimeOffset now)
    {
        EnsureNotTerminal();
        Status = IssueStatus.Rejected;
        UpdatedAt = now;
    }

    public void DefineAcceptanceCriteria(string criteria, DateTimeOffset now)
    {
        RequireStatus("issue.define_criteria_invalid", IssueStatus.Backlog);
        if (string.IsNullOrWhiteSpace(criteria))
            throw new DomainException("issue.criteria_empty", "Acceptance criteria must not be empty.");
        if (criteria.Length > 2000)
            throw new DomainException("issue.criteria_too_long", "Acceptance criteria exceeds 2000 characters.");
        AcceptanceCriteria = criteria;
        Status = IssueStatus.Ready;
        UpdatedAt = now;
    }

    public void Block(DateTimeOffset now)
    {
        RequireStatus("issue.block_invalid", IssueStatus.Ready, IssueStatus.InProgress);
        Status = IssueStatus.Blocked;
        UpdatedAt = now;
    }

    public void Unblock(DateTimeOffset now)
    {
        RequireStatus("issue.unblock_invalid", IssueStatus.Blocked);
        Status = IssueStatus.Ready;
        UpdatedAt = now;
    }

    public void StartWork(DateTimeOffset now)
    {
        RequireStatus("issue.start_work_invalid", IssueStatus.Ready);
        Status = IssueStatus.InProgress;
        UpdatedAt = now;
    }

    public void SubmitForReview(DateTimeOffset now)
    {
        RequireStatus("issue.submit_review_invalid", IssueStatus.InProgress);
        Status = IssueStatus.InReview;
        UpdatedAt = now;
    }

    public void RequestChanges(DateTimeOffset now)
    {
        RequireStatus("issue.request_changes_invalid", IssueStatus.InReview);
        Status = IssueStatus.InProgress;
        UpdatedAt = now;
    }

    public void Complete(DateTimeOffset now)
    {
        RequireStatus("issue.complete_invalid", IssueStatus.InReview);
        Status = IssueStatus.Merged;
        UpdatedAt = now;
    }

    public void Publish(DateTimeOffset now)
    {
        RequireStatus("issue.publish_invalid", IssueStatus.Merged);
        Status = IssueStatus.Published;
        UpdatedAt = now;
    }

    public void Close(DateTimeOffset now)
    {
        EnsureNotTerminal();
        Status = IssueStatus.Closed;
        UpdatedAt = now;
    }

    // --- external case reference ------------------------------------------------------------

    public void LinkExternalCase(ExternalCaseRef caseRef, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(caseRef);
        ExternalCase = caseRef;
        UpdatedAt = now;
    }

    public void UnlinkExternalCase(DateTimeOffset now)
    {
        ExternalCase = null;
        UpdatedAt = now;
    }

    // --- helpers ----------------------------------------------------------------------------

    private void RequireStatus(string code, params IssueStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            var allowedStr = string.Join(", ", allowed.Select(s => s.ToString()));
            throw new DomainException(code,
                $"Transition not allowed from status '{Status}'. Expected one of: {allowedStr}.");
        }
    }

    private void EnsureNotTerminal()
    {
        if (TerminalStatuses.Contains(Status))
        {
            throw new DomainException("issue.terminal",
                $"Issue is in terminal state '{Status}' and cannot be modified.");
        }
    }
}