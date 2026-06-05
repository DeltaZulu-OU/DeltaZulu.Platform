using Dapper;
using Workbench.Application.Abstractions;
using Workbench.Domain.Changes;
using Workbench.Domain.Enums;
using Workbench.Domain.Identifiers;
using Workbench.Domain.Reviews;

namespace Workbench.Persistence.Repositories;

internal sealed class ChangeRequestRepository(DapperSession session) : IChangeRequestRepository
{
    public async Task<ChangeRequest?> GetByIdAsync(ChangeRequestId id, CancellationToken ct = default)
    {
        var idStr = id.Value.ToString();
        var row = await session.Connection.QuerySingleOrDefaultAsync<ChangeRow>(
            "SELECT * FROM change_requests WHERE id = @Id",
            new { Id = idStr }, session.Transaction);
        if (row is null) return null;
        return await HydrateAsync(row);
    }

    public async Task<IReadOnlyList<ChangeRequest>> ListByDetectionAsync(DetectionId detectionId, CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<ChangeRow>(
            "SELECT * FROM change_requests WHERE detection_id = @DetId ORDER BY updated_at DESC",
            new { DetId = detectionId.Value.ToString() }, session.Transaction);
        var result = new List<ChangeRequest>();
        foreach (var row in rows) result.Add(await HydrateAsync(row));
        return result;
    }

    public async Task<IReadOnlyList<ChangeRequest>> ListAsync(CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<ChangeRow>(
            "SELECT * FROM change_requests ORDER BY updated_at DESC",
            transaction: session.Transaction);
        var result = new List<ChangeRequest>();
        foreach (var row in rows) result.Add(await HydrateAsync(row));
        return result;
    }

    public async Task<IReadOnlyList<ChangeRequest>> ListOpenByAuthorAsync(UserId authorId, CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<ChangeRow>(
            "SELECT * FROM change_requests WHERE author_id = @AuthorId AND status NOT IN ('Merged', 'Closed') ORDER BY updated_at DESC",
            new { AuthorId = authorId.Value.ToString() }, session.Transaction);
        var result = new List<ChangeRequest>();
        foreach (var row in rows) result.Add(await HydrateAsync(row));
        return result;
    }

    public async Task<IReadOnlyList<ChangeRequest>> ListAwaitingReviewAsync(UserId excludeAuthorId, CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<ChangeRow>(
            "SELECT * FROM change_requests WHERE status = 'ReviewRequired' AND author_id != @ExcludeAuthorId ORDER BY updated_at DESC",
            new { ExcludeAuthorId = excludeAuthorId.Value.ToString() }, session.Transaction);
        var result = new List<ChangeRequest>();
        foreach (var row in rows) result.Add(await HydrateAsync(row));
        return result;
    }

    public async Task<IReadOnlyList<ChangeRequest>> ListWithFailedBlockingChecksAsync(CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<ChangeRow>("""
            SELECT DISTINCT cr.* FROM change_requests cr
            INNER JOIN check_runs ck ON ck.change_request_id = cr.id
            WHERE cr.status NOT IN ('Merged', 'Closed')
              AND ck.is_blocking = 1
              AND ck.status = 'Failed'
            ORDER BY cr.updated_at DESC
            """, transaction: session.Transaction);
        var result = new List<ChangeRequest>();
        foreach (var row in rows) result.Add(await HydrateAsync(row));
        return result;
    }

    public void Add(ChangeRequest change)
    {
        session.Connection.Execute("""
            INSERT INTO change_requests (id, key, title, detection_id, author_id,
                workflow_profile_id, base_version_id, status, is_stale, stale_reason,
                linked_issue_id, created_at, updated_at, merged_at, result_version_id, close_reason)
            VALUES (@Id, @Key, @Title, @DetectionId, @AuthorId, @WorkflowProfileId,
                @BaseVersionId, @Status, @IsStale, @StaleReason, @LinkedIssueId,
                @CreatedAt, @UpdatedAt, @MergedAt, @ResultVersionId, @CloseReason)
            """, ToParams(change), session.Transaction);
    }

    public void Save(ChangeRequest change)
    {
        // Upsert the change header.
        session.Connection.Execute("""
            UPDATE change_requests SET status = @Status, is_stale = @IsStale,
                stale_reason = @StaleReason, linked_issue_id = @LinkedIssueId,
                workflow_profile_id = @WorkflowProfileId,
                updated_at = @UpdatedAt, merged_at = @MergedAt,
                result_version_id = @ResultVersionId, close_reason = @CloseReason
            WHERE id = @Id
            """, ToParams(change), session.Transaction);

        var idStr = change.Id.Value.ToString();

        // Replace draft files.
        session.Connection.Execute(
            "DELETE FROM change_draft_files WHERE change_request_id = @Id",
            new { Id = idStr }, session.Transaction);
        foreach (var f in change.DraftFiles)
        {
            session.Connection.Execute("""
                INSERT INTO change_draft_files (change_request_id, logical_path, content_type,
                    content, updated_at, updated_by)
                VALUES (@ChangeRequestId, @LogicalPath, @ContentType, @Content, @UpdatedAt, @UpdatedBy)
                """, new
            {
                ChangeRequestId = idStr,
                LogicalPath = f.Path.Value,
                ContentType = f.ContentType.ToString(),
                f.Content,
                UpdatedAt = f.UpdatedAt.ToString("O"),
                UpdatedBy = f.UpdatedBy.Value.ToString(),
            }, session.Transaction);
        }

        // Replace checks.
        session.Connection.Execute(
            "DELETE FROM check_runs WHERE change_request_id = @Id",
            new { Id = idStr }, session.Transaction);
        foreach (var c in change.Checks)
        {
            session.Connection.Execute("""
                INSERT INTO check_runs (id, change_request_id, name, is_blocking, status,
                    started_at, completed_at, summary, details_json, logs_excerpt)
                VALUES (@Id, @ChangeRequestId, @Name, @IsBlocking, @Status,
                    @StartedAt, @CompletedAt, @Summary, @DetailsJson, @LogsExcerpt)
                """, new
            {
                Id = c.Id.Value.ToString(),
                ChangeRequestId = idStr,
                c.Name,
                IsBlocking = c.IsBlocking ? 1 : 0,
                Status = c.Status.ToString(),
                StartedAt = c.StartedAt?.ToString("O"),
                CompletedAt = c.CompletedAt?.ToString("O"),
                c.Summary,
                c.DetailsJson,
                c.LogsExcerpt,
            }, session.Transaction);
        }

        // Replace reviews.
        session.Connection.Execute(
            "DELETE FROM reviews WHERE change_request_id = @Id",
            new { Id = idStr }, session.Transaction);
        foreach (var r in change.Reviews)
        {
            session.Connection.Execute("""
                INSERT INTO reviews (id, change_request_id, reviewer_id, decision, comment,
                    created_at, is_superseded, superseded_at)
                VALUES (@Id, @ChangeRequestId, @ReviewerId, @Decision, @Comment,
                    @CreatedAt, @IsSuperseded, @SupersededAt)
                """, new
            {
                Id = r.Id.Value.ToString(),
                ChangeRequestId = idStr,
                ReviewerId = r.ReviewerId.Value.ToString(),
                Decision = r.Decision.ToString(),
                r.Comment,
                CreatedAt = r.CreatedAt.ToString("O"),
                IsSuperseded = r.IsSuperseded ? 1 : 0,
                SupersededAt = r.SupersededAt?.ToString("O"),
            }, session.Transaction);
        }
    }

    private async Task<ChangeRequest> HydrateAsync(ChangeRow row)
    {
        var idStr = row.id;

        var draftRows = await session.Connection.QueryAsync<DraftFileRow>(
            "SELECT * FROM change_draft_files WHERE change_request_id = @Id",
            new { Id = idStr }, session.Transaction);

        var checkRows = await session.Connection.QueryAsync<CheckRunRow>(
            "SELECT * FROM check_runs WHERE change_request_id = @Id",
            new { Id = idStr }, session.Transaction);

        var reviewRows = await session.Connection.QueryAsync<ReviewRow>(
            "SELECT * FROM reviews WHERE change_request_id = @Id ORDER BY created_at",
            new { Id = idStr }, session.Transaction);

        return row.ToDomain(
            draftRows.Select(d => d.ToDomain()),
            checkRows.Select(c => c.ToDomain()),
            reviewRows.Select(r => r.ToDomain()));
    }

    private static object ToParams(ChangeRequest c) => new
    {
        Id = c.Id.Value.ToString(),
        c.Key,
        c.Title,
        DetectionId = c.DetectionId.Value.ToString(),
        AuthorId = c.AuthorId.Value.ToString(),
        WorkflowProfileId = c.WorkflowProfileId.ToString(),
        BaseVersionId = c.BaseVersionId?.Value.ToString(),
        Status = c.Status.ToString(),
        IsStale = c.IsStale ? 1 : 0,
        c.StaleReason,
        LinkedIssueId = c.LinkedIssueId?.Value.ToString(),
        CreatedAt = c.CreatedAt.ToString("O"),
        UpdatedAt = c.UpdatedAt.ToString("O"),
        MergedAt = c.MergedAt?.ToString("O"),
        ResultVersionId = c.ResultVersionId?.Value.ToString(),
        c.CloseReason,
    };

    // --- Row types ---

    internal sealed class ChangeRow
    {
        public string id { get; set; } = "";
        public string key { get; set; } = "";
        public string title { get; set; } = "";
        public string detection_id { get; set; } = "";
        public string author_id { get; set; } = "";
        public string workflow_profile_id { get; set; } = "";
        public string? base_version_id { get; set; }
        public string status { get; set; } = "";
        public long is_stale { get; set; }
        public string? stale_reason { get; set; }
        public string? linked_issue_id { get; set; }
        public string created_at { get; set; } = "";
        public string updated_at { get; set; } = "";
        public string? merged_at { get; set; }
        public string? result_version_id { get; set; }
        public string? close_reason { get; set; }

        public ChangeRequest ToDomain(
            IEnumerable<ChangeDraftFile> drafts,
            IEnumerable<CheckRun> checks,
            IEnumerable<Review> reviews)
            => ChangeRequest.Reconstitute(
                new ChangeRequestId(Guid.Parse(id)), key, title,
                new DetectionId(Guid.Parse(detection_id)),
                new UserId(Guid.Parse(author_id)),
                Enum.Parse<WorkflowProfileId>(workflow_profile_id),
                base_version_id is not null ? new VersionId(Guid.Parse(base_version_id)) : null,
                Enum.Parse<ChangeStatus>(status),
                is_stale != 0, stale_reason,
                linked_issue_id is not null ? new IssueId(Guid.Parse(linked_issue_id)) : null,
                DateTimeOffset.Parse(created_at), DateTimeOffset.Parse(updated_at),
                merged_at is not null ? DateTimeOffset.Parse(merged_at) : null,
                result_version_id is not null ? new VersionId(Guid.Parse(result_version_id)) : null,
                close_reason, drafts, checks, reviews);
    }

    internal sealed class DraftFileRow
    {
        public string change_request_id { get; set; } = "";
        public string logical_path { get; set; } = "";
        public string content_type { get; set; } = "";
        public string content { get; set; } = "";
        public string updated_at { get; set; } = "";
        public string updated_by { get; set; } = "";

        public ChangeDraftFile ToDomain()
        {
            var crId = new ChangeRequestId(Guid.Parse(change_request_id));
            var path = LogicalPath.Parse(logical_path);
            return new ChangeDraftFile(crId, path,
                Enum.Parse<DraftContentType>(content_type),
                content, new UserId(Guid.Parse(updated_by)),
                DateTimeOffset.Parse(updated_at));
        }
    }

    internal sealed class CheckRunRow
    {
        public string id { get; set; } = "";
        public string change_request_id { get; set; } = "";
        public string name { get; set; } = "";
        public long is_blocking { get; set; }
        public string status { get; set; } = "";
        public string? started_at { get; set; }
        public string? completed_at { get; set; }
        public string summary { get; set; } = "";
        public string details_json { get; set; } = "";
        public string logs_excerpt { get; set; } = "";

        public CheckRun ToDomain() => CheckRun.Reconstitute(
            new CheckRunId(Guid.Parse(id)),
            new ChangeRequestId(Guid.Parse(change_request_id)),
            name, is_blocking != 0,
            Enum.Parse<CheckStatus>(status),
            started_at is not null ? DateTimeOffset.Parse(started_at) : null,
            completed_at is not null ? DateTimeOffset.Parse(completed_at) : null,
            summary, details_json, logs_excerpt);
    }

    internal sealed class ReviewRow
    {
        public string id { get; set; } = "";
        public string change_request_id { get; set; } = "";
        public string reviewer_id { get; set; } = "";
        public string decision { get; set; } = "";
        public string comment { get; set; } = "";
        public string created_at { get; set; } = "";
        public long is_superseded { get; set; }
        public string? superseded_at { get; set; }

        public Review ToDomain() => Review.Reconstitute(
            new ReviewId(Guid.Parse(id)),
            new ChangeRequestId(Guid.Parse(change_request_id)),
            new UserId(Guid.Parse(reviewer_id)),
            Enum.Parse<ReviewDecision>(decision),
            comment, DateTimeOffset.Parse(created_at),
            is_superseded != 0,
            superseded_at is not null ? DateTimeOffset.Parse(superseded_at) : null);
    }
}
