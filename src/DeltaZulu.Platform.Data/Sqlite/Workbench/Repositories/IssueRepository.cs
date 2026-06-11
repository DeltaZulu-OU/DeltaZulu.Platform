using Dapper;
using DeltaZulu.Platform.Application.Workbench.Abstractions;
using DeltaZulu.Platform.Domain.Workbench.Enums;
using DeltaZulu.Platform.Domain.Workbench.Identifiers;
using DeltaZulu.Platform.Domain.Workbench.Issues;

namespace DeltaZulu.Platform.Data.Workbench.Repositories;

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

    public async Task<IReadOnlyList<Issue>> ListOpenAsync(CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<IssueRow>(
            "SELECT * FROM issues WHERE status NOT IN ('Rejected','Closed') ORDER BY updated_at DESC",
            transaction: session.Transaction);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public void Add(Issue issue)
    {
        session.Connection.Execute("""
            INSERT INTO issues (id, key, title, type, status,
                ext_case_system, ext_case_external_id, ext_case_url, ext_case_system_type,
                description, acceptance_criteria, data_source, platform, attack_technique_id,
                tlp, labels, created_at, updated_at)
            VALUES (@Id, @Key, @Title, @Type, @Status,
                @ExtCaseSystem, @ExtCaseExternalId, @ExtCaseUrl, @ExtCaseSystemType,
                @Description, @AcceptanceCriteria, @DataSource, @Platform, @AttackTechniqueId,
                @Tlp, @Labels, @CreatedAt, @UpdatedAt)
            """,
            ToParams(issue),
            session.Transaction);
    }

    public void Save(Issue issue)
    {
        session.Connection.Execute("""
            UPDATE issues SET title = @Title, status = @Status,
                ext_case_system = @ExtCaseSystem, ext_case_external_id = @ExtCaseExternalId,
                ext_case_url = @ExtCaseUrl, ext_case_system_type = @ExtCaseSystemType,
                description = @Description, acceptance_criteria = @AcceptanceCriteria,
                data_source = @DataSource, platform = @Platform,
                attack_technique_id = @AttackTechniqueId,
                tlp = @Tlp, labels = @Labels,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """,
            ToParams(issue),
            session.Transaction);
    }

    private static object ToParams(Issue i) => new {
        Id = i.Id.Value.ToString(),
        i.Key,
        i.Title,
        Type = i.Type.ToString(),
        Status = i.Status.ToString(),
        ExtCaseSystem = i.ExternalCase?.System,
        ExtCaseExternalId = i.ExternalCase?.ExternalId,
        ExtCaseUrl = i.ExternalCase?.Url,
        ExtCaseSystemType = (int)(i.ExternalCase?.SystemType ?? ExternalSystemType.Generic),
        i.Description,
        AcceptanceCriteria = i.AcceptanceCriteria,
        DataSource = i.DataSource,
        i.Platform,
        AttackTechniqueId = i.AttackTechniqueId,
        Tlp = i.Tlp?.ToString(),
        Labels = string.Join("|", i.Labels),
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
        public string? ext_case_system { get; set; }
        public string? ext_case_external_id { get; set; }
        public string? ext_case_url { get; set; }
        public int ext_case_system_type { get; set; }
        public string? description { get; set; }
        public string? acceptance_criteria { get; set; }
        public string? data_source { get; set; }
        public string? platform { get; set; }
        public string? attack_technique_id { get; set; }
        public string? tlp { get; set; }
        public string labels { get; set; } = "";
        public string created_at { get; set; } = "";
        public string updated_at { get; set; } = "";

        public Issue ToDomain()
        {
            ExternalCaseRef? extCase = ext_case_system is not null && ext_case_external_id is not null
                ? new ExternalCaseRef(ext_case_system, ext_case_external_id, ext_case_url,
                    (ExternalSystemType)ext_case_system_type)
                : null;

            TlpLevel? tlpLevel = tlp is not null ? Enum.Parse<TlpLevel>(tlp) : null;

            var labelList = string.IsNullOrEmpty(labels)
                ? []
                : (IReadOnlyList<string>)labels.Split('|', StringSplitOptions.RemoveEmptyEntries);

            return Issue.Reconstitute(
                new IssueId(Guid.Parse(id)), key, title,
                Enum.Parse<IssueType>(type),
                ParseStatus(status),
                extCase,
                DateTimeOffset.Parse(created_at),
                DateTimeOffset.Parse(updated_at),
                description, acceptance_criteria, data_source, platform, attack_technique_id,
                tlpLevel, labelList);
        }

        private static IssueStatus ParseStatus(string status) => status switch
        {
            // Legacy 5-state issue lifecycle values retained for databases seeded or created
            // before ADR-0018 expanded the issue workflow state machine.
            "Open" => IssueStatus.New,
            "Resolved" => IssueStatus.Merged,
            _ => Enum.Parse<IssueStatus>(status),
        };
    }
}