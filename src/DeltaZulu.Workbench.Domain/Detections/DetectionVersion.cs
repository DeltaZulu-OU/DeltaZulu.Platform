using DeltaZulu.Workbench.Domain.Common;
using DeltaZulu.Workbench.Domain.Enums;
using DeltaZulu.Workbench.Domain.Identifiers;

namespace DeltaZulu.Workbench.Domain.Detections;

/// <summary>
/// A user-facing projection of a Git commit, per ADR-0011. Immutable after creation.
/// </summary>
public sealed class DetectionVersion : Entity<VersionId>
{
    public DetectionId DetectionId { get; }
    public int SequenceNumber { get; }
    public string DisplayVersion { get; }
    public string Title { get; }
    public string ChangeSummary { get; }
    public UserId AuthorId { get; }
    public WorkflowProfileId WorkflowProfile { get; }
    public ChangeRequestId SourceChangeRequestId { get; }
    public IssueId? LinkedIssueId { get; }
    public DateTimeOffset AcceptedAt { get; }
    public IReadOnlyList<LogicalPath> ChangedSections { get; }
    public string GitCommitSha { get; }
    public string ChecksSummary { get; }
    public string ReviewSummary { get; }

    private DetectionVersion(
        VersionId id, DetectionId detectionId, int sequenceNumber,
        string title, string changeSummary, UserId authorId,
        WorkflowProfileId workflowProfile, ChangeRequestId sourceChangeRequestId,
        IssueId? linkedIssueId, DateTimeOffset acceptedAt,
        IReadOnlyList<LogicalPath> changedSections, string gitCommitSha,
        string checksSummary, string reviewSummary)
        : base(id)
    {
        DetectionId = detectionId;
        SequenceNumber = sequenceNumber;
        DisplayVersion = $"v{sequenceNumber}";
        Title = title;
        ChangeSummary = changeSummary;
        AuthorId = authorId;
        WorkflowProfile = workflowProfile;
        SourceChangeRequestId = sourceChangeRequestId;
        LinkedIssueId = linkedIssueId;
        AcceptedAt = acceptedAt;
        ChangedSections = changedSections;
        GitCommitSha = gitCommitSha;
        ChecksSummary = checksSummary;
        ReviewSummary = reviewSummary;
    }

    public static DetectionVersion Project(
        VersionId id, DetectionId detectionId, int sequenceNumber,
        string title, string changeSummary, UserId authorId,
        WorkflowProfileId workflowProfile, ChangeRequestId sourceChangeRequestId,
        IssueId? linkedIssueId, DateTimeOffset acceptedAt,
        IReadOnlyList<LogicalPath> changedSections, string gitCommitSha,
        string checksSummary, string reviewSummary)
    {
        if (sequenceNumber < 1)
        {
            throw new DomainException("version.sequence_invalid",
                "Detection version sequence number must be 1 or greater.");
        }

        if (string.IsNullOrWhiteSpace(gitCommitSha))
        {
            throw new DomainException("version.commit_empty",
                "Git commit SHA must accompany an accepted version projection.");
        }

        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(changeSummary);
        ArgumentNullException.ThrowIfNull(changedSections);
        ArgumentNullException.ThrowIfNull(checksSummary);
        ArgumentNullException.ThrowIfNull(reviewSummary);

        return new DetectionVersion(id, detectionId, sequenceNumber, title, changeSummary,
            authorId, workflowProfile, sourceChangeRequestId, linkedIssueId, acceptedAt,
            changedSections, gitCommitSha, checksSummary, reviewSummary);
    }

    /// <summary>Reconstitutes from persistence. No validation.</summary>
    public static DetectionVersion Reconstitute(
        VersionId id, DetectionId detectionId, int sequenceNumber,
        string title, string changeSummary, UserId authorId,
        WorkflowProfileId workflowProfile, ChangeRequestId sourceChangeRequestId,
        IssueId? linkedIssueId, DateTimeOffset acceptedAt,
        IReadOnlyList<LogicalPath> changedSections, string gitCommitSha,
        string checksSummary, string reviewSummary)
    {
        return new DetectionVersion(id, detectionId, sequenceNumber, title, changeSummary,
            authorId, workflowProfile, sourceChangeRequestId, linkedIssueId, acceptedAt,
            changedSections, gitCommitSha, checksSummary, reviewSummary);
    }
}