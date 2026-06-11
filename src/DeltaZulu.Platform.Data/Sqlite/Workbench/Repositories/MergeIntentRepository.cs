using Dapper;
using DeltaZulu.Platform.Application.Workbench.Abstractions;
using DeltaZulu.Platform.Domain.Workbench.Identifiers;

namespace DeltaZulu.Platform.Data.Workbench.Repositories;

internal sealed class MergeIntentRepository(DapperSession session) : IMergeIntentRepository
{
    public async Task CreatePendingAsync(MergeIntent intent, CancellationToken ct = default)
    {
        await session.Connection.ExecuteAsync("""
            INSERT INTO merge_intents (change_request_id, detection_id, detection_slug,
                requested_at, author_name, author_email, commit_message, state)
            VALUES (@ChangeId, @DetectionId, @DetectionSlug, @RequestedAt, @AuthorName,
                @AuthorEmail, @CommitMessage, @State)
            ON CONFLICT(change_request_id) DO UPDATE SET
                detection_id = excluded.detection_id,
                detection_slug = excluded.detection_slug,
                requested_at = excluded.requested_at,
                author_name = excluded.author_name,
                author_email = excluded.author_email,
                commit_message = excluded.commit_message,
                state = excluded.state,
                commit_sha = NULL,
                committed_at = NULL,
                version_id = NULL,
                completed_at = NULL
            """, ToParameters(intent), session.Transaction);
    }

    public async Task MarkCommittedAsync(
        ChangeRequestId changeId,
        string commitSha,
        DateTimeOffset committedAt,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);

        await session.Connection.ExecuteAsync("""
            UPDATE merge_intents
            SET state = @State,
                commit_sha = @CommitSha,
                committed_at = @CommittedAt
            WHERE change_request_id = @ChangeId
            """, new {
            ChangeId = changeId.Value.ToString(),
            State = MergeIntentState.Committed.ToString(),
            CommitSha = commitSha,
            CommittedAt = committedAt.ToString("O"),
        }, session.Transaction);
    }

    public async Task MarkCompletedAsync(
        ChangeRequestId changeId,
        VersionId versionId,
        DateTimeOffset completedAt,
        CancellationToken ct = default)
    {
        await session.Connection.ExecuteAsync("""
            UPDATE merge_intents
            SET state = @State,
                version_id = @VersionId,
                completed_at = @CompletedAt
            WHERE change_request_id = @ChangeId
            """, new {
            ChangeId = changeId.Value.ToString(),
            State = MergeIntentState.Completed.ToString(),
            VersionId = versionId.Value.ToString(),
            CompletedAt = completedAt.ToString("O"),
        }, session.Transaction);
    }

    public async Task<IReadOnlyList<MergeIntent>> ListUnresolvedAsync(CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<MergeIntentRow>("""
            SELECT *
            FROM merge_intents
            WHERE state <> @Completed
            ORDER BY requested_at
            """, new { Completed = MergeIntentState.Completed.ToString() }, session.Transaction);

        return rows.Select(row => row.ToDomain()).ToArray();
    }

    private static object ToParameters(MergeIntent intent) => new {
        ChangeId = intent.ChangeId.Value.ToString(),
        DetectionId = intent.DetectionId.Value.ToString(),
        intent.DetectionSlug,
        RequestedAt = intent.RequestedAt.ToString("O"),
        intent.AuthorName,
        intent.AuthorEmail,
        intent.CommitMessage,
        State = intent.State.ToString(),
    };

    internal sealed class MergeIntentRow
    {
        public string change_request_id { get; set; } = "";
        public string detection_id { get; set; } = "";
        public string detection_slug { get; set; } = "";
        public string requested_at { get; set; } = "";
        public string author_name { get; set; } = "";
        public string author_email { get; set; } = "";
        public string commit_message { get; set; } = "";
        public string state { get; set; } = "";
        public string? commit_sha { get; set; }
        public string? committed_at { get; set; }
        public string? version_id { get; set; }
        public string? completed_at { get; set; }

        public MergeIntent ToDomain() => new(
            new ChangeRequestId(Guid.Parse(change_request_id)),
            new DetectionId(Guid.Parse(detection_id)),
            detection_slug,
            DateTimeOffset.Parse(requested_at),
            author_name,
            author_email,
            commit_message,
            Enum.Parse<MergeIntentState>(state),
            commit_sha,
            committed_at is null ? null : DateTimeOffset.Parse(committed_at),
            version_id is null ? null : new VersionId(Guid.Parse(version_id)),
            completed_at is null ? null : DateTimeOffset.Parse(completed_at));
    }
}