using Dapper;
using DeltaZulu.Workbench.Application.Abstractions;
using DeltaZulu.Workbench.Domain.Detections;
using DeltaZulu.Workbench.Domain.Enums;
using DeltaZulu.Workbench.Domain.Identifiers;

namespace DeltaZulu.Workbench.Persistence.Repositories;

internal sealed class DetectionVersionRepository(DapperSession session) : IDetectionVersionRepository
{
    public async Task<DetectionVersion?> GetByIdAsync(VersionId id, CancellationToken ct = default)
    {
        var row = await session.Connection.QuerySingleOrDefaultAsync<VersionRow>(
            "SELECT * FROM detection_versions WHERE id = @Id",
            new { Id = id.Value.ToString() }, session.Transaction);
        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<DetectionVersion>> ListByDetectionAsync(DetectionId detectionId, CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<VersionRow>(
            "SELECT * FROM detection_versions WHERE detection_id = @DetId ORDER BY sequence_number DESC",
            new { DetId = detectionId.Value.ToString() }, session.Transaction);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<int> GetNextSequenceNumberAsync(DetectionId detectionId, CancellationToken ct = default)
    {
        var max = await session.Connection.ExecuteScalarAsync<int?>(
            "SELECT MAX(sequence_number) FROM detection_versions WHERE detection_id = @DetId",
            new { DetId = detectionId.Value.ToString() }, session.Transaction);
        return (max ?? 0) + 1;
    }

    public async Task<IReadOnlyList<DetectionVersion>> ListRecentAsync(int count, CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<VersionRow>(
            "SELECT * FROM detection_versions ORDER BY accepted_at DESC LIMIT @Count",
            new { Count = count }, session.Transaction);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public void Add(DetectionVersion version)
    {
        session.Connection.Execute("""
            INSERT INTO detection_versions (id, detection_id, sequence_number, display_version,
                title, change_summary, author_id, workflow_profile, source_change_request_id,
                linked_issue_id, accepted_at, changed_sections, git_commit_sha,
                checks_summary, review_summary)
            VALUES (@Id, @DetectionId, @SequenceNumber, @DisplayVersion, @Title, @ChangeSummary,
                @AuthorId, @WorkflowProfile, @SourceChangeRequestId, @LinkedIssueId,
                @AcceptedAt, @ChangedSections, @GitCommitSha, @ChecksSummary, @ReviewSummary)
            """, new
        {
            Id = version.Id.Value.ToString(),
            DetectionId = version.DetectionId.Value.ToString(),
            version.SequenceNumber,
            version.DisplayVersion,
            version.Title,
            version.ChangeSummary,
            AuthorId = version.AuthorId.Value.ToString(),
            WorkflowProfile = version.WorkflowProfile.ToString(),
            SourceChangeRequestId = version.SourceChangeRequestId.Value.ToString(),
            LinkedIssueId = version.LinkedIssueId?.Value.ToString(),
            AcceptedAt = version.AcceptedAt.ToString("O"),
            ChangedSections = string.Join(';', version.ChangedSections.Select(p => p.Value)),
            version.GitCommitSha,
            version.ChecksSummary,
            version.ReviewSummary,
        }, session.Transaction);
    }

    internal sealed class VersionRow
    {
        public string id { get; set; } = "";
        public string detection_id { get; set; } = "";
        public int sequence_number { get; set; }
        public string display_version { get; set; } = "";
        public string title { get; set; } = "";
        public string change_summary { get; set; } = "";
        public string author_id { get; set; } = "";
        public string workflow_profile { get; set; } = "";
        public string source_change_request_id { get; set; } = "";
        public string? linked_issue_id { get; set; }
        public string accepted_at { get; set; } = "";
        public string changed_sections { get; set; } = "";
        public string git_commit_sha { get; set; } = "";
        public string checks_summary { get; set; } = "";
        public string review_summary { get; set; } = "";

        public DetectionVersion ToDomain()
        {
            var sections = string.IsNullOrEmpty(changed_sections)
                ? Array.Empty<LogicalPath>()
                : changed_sections.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(LogicalPath.Parse).ToArray();

            return DetectionVersion.Reconstitute(
                new VersionId(Guid.Parse(id)),
                new DetectionId(Guid.Parse(detection_id)),
                sequence_number, title, change_summary,
                new UserId(Guid.Parse(author_id)),
                Enum.Parse<WorkflowProfileId>(workflow_profile),
                new ChangeRequestId(Guid.Parse(source_change_request_id)),
                linked_issue_id is not null ? new IssueId(Guid.Parse(linked_issue_id)) : null,
                DateTimeOffset.Parse(accepted_at),
                sections, git_commit_sha, checks_summary, review_summary);
        }
    }
}
