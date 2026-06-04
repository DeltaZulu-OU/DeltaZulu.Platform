using Workbench.Domain.Common;
using Workbench.Domain.Enums;
using Workbench.Domain.Identifiers;

namespace Workbench.Domain.Issues;

/// <summary>
/// Issue aggregate root. Issues track detection content work. Case management lives in
/// external systems (FlowIntel, TheHive); issues link via <see cref="ExternalCaseRef"/>.
/// </summary>
public sealed class Issue : Entity<IssueId>
{
    public string Key { get; }
    public string Title { get; private set; }
    public IssueType Type { get; }
    public IssueStatus Status { get; private set; }
    public Priority Priority { get; }
    public UserId? AssigneeId { get; }
    public DetectionId? LinkedDetectionId { get; private set; }
    public ExternalCaseRef? ExternalCase { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Issue(
        IssueId id, string key, string title, IssueType type, Priority priority,
        UserId? assigneeId, DateTimeOffset createdAt)
        : base(id)
    {
        Key = key;
        Title = title;
        Type = type;
        Priority = priority;
        AssigneeId = assigneeId;
        Status = IssueStatus.Open;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static Issue Create(
        IssueId id, string key, string title, IssueType type, Priority priority,
        DateTimeOffset now, UserId? assigneeId = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainException("issue.key_empty", "Issue key must not be empty.");
        if (key.Length > 32)
            throw new DomainException("issue.key_too_long", "Issue key exceeds 32 characters.");
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("issue.title_empty", "Issue title must not be empty.");
        if (title.Length > 200)
            throw new DomainException("issue.title_too_long", "Issue title exceeds 200 characters.");

        return new Issue(id, key, title, type, priority, assigneeId, now);
    }

    internal static Issue Reconstitute(
        IssueId id, string key, string title, IssueType type, IssueStatus status,
        Priority priority, UserId? assigneeId, DetectionId? linkedDetectionId,
        ExternalCaseRef? externalCase, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        var i = new Issue(id, key, title, type, priority, assigneeId, createdAt);
        i.Status = status;
        i.LinkedDetectionId = linkedDetectionId;
        i.ExternalCase = externalCase;
        i.UpdatedAt = updatedAt;
        return i;
    }

    public void Rename(string newTitle, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
            throw new DomainException("issue.title_empty", "Issue title must not be empty.");
        if (newTitle.Length > 200)
            throw new DomainException("issue.title_too_long", "Issue title exceeds 200 characters.");
        Title = newTitle;
        UpdatedAt = now;
    }

    public void TransitionStatus(IssueStatus next, DateTimeOffset now)
    {
        if (Status == IssueStatus.Closed && next != IssueStatus.Closed)
            throw new DomainException("issue.reopen_forbidden",
                "A closed issue cannot be reopened. Create a new issue if needed.");
        Status = next;
        UpdatedAt = now;
    }

    public void LinkDetection(DetectionId detectionId, DateTimeOffset now)
    {
        LinkedDetectionId = detectionId;
        UpdatedAt = now;
    }

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
}