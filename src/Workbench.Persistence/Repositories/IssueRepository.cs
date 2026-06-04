using Dapper;
using Workbench.Application.Abstractions;
using Workbench.Domain.Enums;
using Workbench.Domain.Identifiers;
using Workbench.Domain.Issues;

namespace Workbench.Persistence.Repositories;

internal sealed class IssueRepository(DapperSession session) : IIssueRepository
{
    public async Task<Issue?> GetByIdAsync(IssueId id, CancellationToken ct = default)
    {
        var row = await session.Connection.QuerySingleOrDefaultAsync<IssueRow>(
            "SELECT * FROM issues WHERE id = @Id",
            new { Id = id.Value.ToString() },
            session.Transaction);
        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<Issue>> ListAsync(CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<IssueRow>(
            "SELECT * FROM issues ORDER BY updated_at DESC",
            transaction: session.Transaction);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public void Add(Issue issue)
    {
        session.Connection.Execute("""
            INSERT INTO issues (id, key, title, type, status, priority, assignee_id,
                linked_detection_id, ext_case_system, ext_case_external_id, ext_case_url,
                created_at, updated_at)
            VALUES (@Id, @Key, @Title, @Type, @Status, @Priority, @AssigneeId,
                @LinkedDetectionId, @ExtCaseSystem, @ExtCaseExternalId, @ExtCaseUrl,
                @CreatedAt, @UpdatedAt)
            """,
            ToParams(issue),
            session.Transaction);
    }

    public void Save(Issue issue)
    {
        session.Connection.Execute("""
            UPDATE issues SET title = @Title, status = @Status,
                linked_detection_id = @LinkedDetectionId,
                ext_case_system = @ExtCaseSystem, ext_case_external_id = @ExtCaseExternalId,
                ext_case_url = @ExtCaseUrl, updated_at = @UpdatedAt
            WHERE id = @Id
            """,
            ToParams(issue),
            session.Transaction);
    }

    private static object ToParams(Issue i) => new
    {
        Id = i.Id.Value.ToString(),
        i.Key,
        i.Title,
        Type = i.Type.ToString(),
        Status = i.Status.ToString(),
        Priority = i.Priority.ToString(),
        AssigneeId = i.AssigneeId?.Value.ToString(),
        LinkedDetectionId = i.LinkedDetectionId?.Value.ToString(),
        ExtCaseSystem = i.ExternalCase?.System,
        ExtCaseExternalId = i.ExternalCase?.ExternalId,
        ExtCaseUrl = i.ExternalCase?.Url,
        CreatedAt = i.CreatedAt.ToString("O"),
        UpdatedAt = i.UpdatedAt.ToString("O"),
    };

    internal sealed class IssueRow
    {
        public string id { get; set; } = "";
        public string key { get; set; } = "";
        public string title { get; set; } = "";
        public string type { get; set; } = "";
        public string status { get; set; } = "";
        public string priority { get; set; } = "";
        public string? assignee_id { get; set; }
        public string? linked_detection_id { get; set; }
        public string? ext_case_system { get; set; }
        public string? ext_case_external_id { get; set; }
        public string? ext_case_url { get; set; }
        public string created_at { get; set; } = "";
        public string updated_at { get; set; } = "";

        public Issue ToDomain()
        {
            ExternalCaseRef? extCase = ext_case_system is not null && ext_case_external_id is not null
                ? new ExternalCaseRef(ext_case_system, ext_case_external_id, ext_case_url)
                : null;

            return Issue.Reconstitute(
                new IssueId(Guid.Parse(id)), key, title,
                Enum.Parse<IssueType>(type),
                Enum.Parse<IssueStatus>(status),
                Enum.Parse<Priority>(priority),
                assignee_id is not null ? new UserId(Guid.Parse(assignee_id)) : null,
                linked_detection_id is not null ? new DetectionId(Guid.Parse(linked_detection_id)) : null,
                extCase,
                DateTimeOffset.Parse(created_at),
                DateTimeOffset.Parse(updated_at));
        }
    }
}