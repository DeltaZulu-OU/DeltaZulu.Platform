using DeltaZulu.Workbench.Domain.Common;
using DeltaZulu.Workbench.Domain.Enums;
using DeltaZulu.Workbench.Domain.Identifiers;

namespace DeltaZulu.Workbench.Domain.Reviews;

/// <summary>
/// A reviewer's decision on a change request. Append-only; may be marked superseded
/// when content is edited after approval under a profile that resets approvals.
/// </summary>
public sealed class Review : Entity<ReviewId>
{
    public ChangeRequestId ChangeRequestId { get; }
    public UserId ReviewerId { get; }
    public ReviewDecision Decision { get; }
    public string Comment { get; }
    public DateTimeOffset CreatedAt { get; }
    public bool IsSuperseded { get; private set; }
    public DateTimeOffset? SupersededAt { get; private set; }

    internal Review(
        ReviewId id, ChangeRequestId changeRequestId, UserId reviewerId,
        ReviewDecision decision, string comment, DateTimeOffset now)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(comment);
        if (comment.Length > 4000)
            throw new DomainException("review.comment_too_long", "Review comment exceeds 4000 characters.");

        ChangeRequestId = changeRequestId;
        ReviewerId = reviewerId;
        Decision = decision;
        Comment = comment;
        CreatedAt = now;
    }

    /// <summary>Reconstitutes from persistence. No validation — data is trusted.</summary>
    internal static Review Reconstitute(
        ReviewId id, ChangeRequestId changeRequestId, UserId reviewerId,
        ReviewDecision decision, string comment, DateTimeOffset createdAt,
        bool isSuperseded, DateTimeOffset? supersededAt)
    {
        return new Review(id, changeRequestId, reviewerId, decision, comment, createdAt, skip: true)
        {
            IsSuperseded = isSuperseded,
            SupersededAt = supersededAt
        };
    }

    // Validation-free path for Reconstitute only.
    private Review(
        ReviewId id, ChangeRequestId changeRequestId, UserId reviewerId,
        ReviewDecision decision, string comment, DateTimeOffset createdAt, bool skip)
        : base(id)
    {
        ChangeRequestId = changeRequestId;
        ReviewerId = reviewerId;
        Decision = decision;
        Comment = comment;
        CreatedAt = createdAt;
    }

    internal void Supersede(DateTimeOffset now)
    {
        if (IsSuperseded) return;
        IsSuperseded = true;
        SupersededAt = now;
    }
}
