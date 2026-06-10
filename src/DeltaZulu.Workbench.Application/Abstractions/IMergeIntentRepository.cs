using DeltaZulu.Workbench.Domain.Identifiers;

namespace DeltaZulu.Workbench.Application.Abstractions;

/// <summary>
/// Stores durable merge intent/outbox records around accepted-content commits so operators can
/// detect Git commits that were written before the database version projection completed.
/// </summary>
public interface IMergeIntentRepository
{
    Task CreatePendingAsync(MergeIntent intent, CancellationToken ct = default);

    Task MarkCommittedAsync(
        ChangeRequestId changeId,
        string commitSha,
        DateTimeOffset committedAt,
        CancellationToken ct = default);

    Task MarkCompletedAsync(
        ChangeRequestId changeId,
        VersionId versionId,
        DateTimeOffset completedAt,
        CancellationToken ct = default);

    Task<IReadOnlyList<MergeIntent>> ListUnresolvedAsync(CancellationToken ct = default);
}

/// <summary>User-facing recovery status for a merge attempt.</summary>
public enum MergeIntentState
{
    Pending,
    Committed,
    Completed,
}

/// <summary>
/// Database-owned recovery marker for a single merge attempt.
/// </summary>
public sealed record MergeIntent(
    ChangeRequestId ChangeId,
    DetectionId DetectionId,
    string DetectionSlug,
    DateTimeOffset RequestedAt,
    string AuthorName,
    string AuthorEmail,
    string CommitMessage,
    MergeIntentState State,
    string? CommitSha = null,
    DateTimeOffset? CommittedAt = null,
    VersionId? VersionId = null,
    DateTimeOffset? CompletedAt = null);