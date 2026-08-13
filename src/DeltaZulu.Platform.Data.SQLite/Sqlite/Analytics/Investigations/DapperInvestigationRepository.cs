using Dapper;
using DeltaZulu.Platform.Domain.Analytics.Investigations;
using static DeltaZulu.Platform.Data.Sqlite.Analytics.SqliteDateTimeHelpers;

namespace DeltaZulu.Platform.Data.Sqlite.Analytics.Investigations;

public sealed class DapperInvestigationRepository : DapperRepositoryBase, IInvestigationRepository
{
    private const string CreateSchemaSql =
        """
        CREATE TABLE IF NOT EXISTS investigations (
            id TEXT PRIMARY KEY,
            title TEXT NOT NULL,
            description TEXT NULL,
            status TEXT NOT NULL,
            created_by TEXT NOT NULL,
            created_at_utc TEXT NOT NULL,
            updated_at_utc TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS investigation_pivots (
            id TEXT PRIMARY KEY,
            investigation_id TEXT NOT NULL,
            name TEXT NOT NULL,
            query_text TEXT NOT NULL,
            description TEXT NULL,
            created_at_utc TEXT NOT NULL,
            updated_at_utc TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS investigation_query_runs (
            id TEXT PRIMARY KEY,
            investigation_id TEXT NOT NULL,
            pivot_id TEXT NOT NULL,
            query_text TEXT NOT NULL,
            started_at_utc TEXT NOT NULL,
            duration_ms INTEGER NOT NULL,
            succeeded INTEGER NOT NULL,
            row_count INTEGER NULL,
            diagnostics_json TEXT NULL,
            result_schema_json TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS investigation_evidence (
            id TEXT PRIMARY KEY,
            investigation_id TEXT NOT NULL,
            query_run_id TEXT NULL,
            source_table TEXT NULL,
            source_reference_json TEXT NULL,
            row_snapshot_json TEXT NOT NULL,
            summary TEXT NULL,
            created_by TEXT NOT NULL,
            created_at_utc TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS investigation_evidence_tags (
            evidence_id TEXT NOT NULL,
            tag TEXT NOT NULL,
            added_by TEXT NOT NULL,
            added_at_utc TEXT NOT NULL,
            PRIMARY KEY (evidence_id, tag)
        );

        CREATE TABLE IF NOT EXISTS investigation_evidence_comments (
            id TEXT PRIMARY KEY,
            evidence_id TEXT NOT NULL,
            body TEXT NOT NULL,
            created_by TEXT NOT NULL,
            created_at_utc TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS investigation_evidence_links (
            id TEXT PRIMARY KEY,
            investigation_id TEXT NOT NULL,
            from_evidence_id TEXT NOT NULL,
            to_evidence_id TEXT NOT NULL,
            relationship TEXT NOT NULL,
            created_by TEXT NOT NULL,
            created_at_utc TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS investigation_evidence_entity_links (
            id TEXT PRIMARY KEY,
            evidence_id TEXT NOT NULL,
            entity_kind TEXT NOT NULL,
            entity_key TEXT NOT NULL,
            entity_display TEXT NULL,
            created_by TEXT NOT NULL,
            created_at_utc TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_investigation_pivots_investigation
            ON investigation_pivots (investigation_id);

        CREATE INDEX IF NOT EXISTS idx_investigation_query_runs_investigation
            ON investigation_query_runs (investigation_id, started_at_utc DESC);

        CREATE INDEX IF NOT EXISTS idx_investigation_evidence_investigation
            ON investigation_evidence (investigation_id, created_at_utc ASC);
        """;

    public DapperInvestigationRepository(IAppDbConnectionFactory connectionFactory)
        : base(connectionFactory, CreateSchemaSql)
    {
    }

    public async Task SaveInvestigationAsync(InvestigationRecord investigation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(investigation);

        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO investigations (id, title, description, status, created_by, created_at_utc, updated_at_utc)
            VALUES (@Id, @Title, @Description, @Status, @CreatedBy, @CreatedAtUtc, @UpdatedAtUtc)
            ON CONFLICT(id) DO UPDATE SET
                title = excluded.title,
                description = excluded.description,
                status = excluded.status,
                updated_at_utc = excluded.updated_at_utc;
            """,
            new
            {
                investigation.Id,
                investigation.Title,
                investigation.Description,
                investigation.Status,
                investigation.CreatedBy,
                CreatedAtUtc = Format(investigation.CreatedAtUtc),
                UpdatedAtUtc = Format(investigation.UpdatedAtUtc)
            },
            cancellationToken: cancellationToken));
    }

    public async Task<InvestigationRecord?> GetInvestigationAsync(string investigationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(investigationId);

        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<InvestigationRow>(new CommandDefinition(
            """
            SELECT id AS Id, title AS Title, description AS Description, status AS Status,
                created_by AS CreatedBy, created_at_utc AS CreatedAtUtc, updated_at_utc AS UpdatedAtUtc
            FROM investigations
            WHERE id = @InvestigationId;
            """,
            new { InvestigationId = investigationId },
            cancellationToken: cancellationToken));

        return rows.Select(ToRecord).SingleOrDefault();
    }

    public async Task<IReadOnlyList<InvestigationRecord>> ListInvestigationsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<InvestigationRow>(new CommandDefinition(
            """
            SELECT id AS Id, title AS Title, description AS Description, status AS Status,
                created_by AS CreatedBy, created_at_utc AS CreatedAtUtc, updated_at_utc AS UpdatedAtUtc
            FROM investigations
            ORDER BY updated_at_utc DESC;
            """,
            cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task SavePivotAsync(InvestigationPivotRecord pivot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pivot);

        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO investigation_pivots (id, investigation_id, name, query_text, description, created_at_utc, updated_at_utc)
            VALUES (@Id, @InvestigationId, @Name, @QueryText, @Description, @CreatedAtUtc, @UpdatedAtUtc)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                query_text = excluded.query_text,
                description = excluded.description,
                updated_at_utc = excluded.updated_at_utc;
            """,
            new { pivot.Id, pivot.InvestigationId, pivot.Name, pivot.QueryText, pivot.Description, CreatedAtUtc = Format(pivot.CreatedAtUtc), UpdatedAtUtc = Format(pivot.UpdatedAtUtc) },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<InvestigationPivotRecord>> ListPivotsAsync(string investigationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(investigationId);

        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PivotRow>(new CommandDefinition(
            """
            SELECT id AS Id, investigation_id AS InvestigationId, name AS Name, query_text AS QueryText,
                description AS Description, created_at_utc AS CreatedAtUtc, updated_at_utc AS UpdatedAtUtc
            FROM investigation_pivots
            WHERE investigation_id = @InvestigationId
            ORDER BY updated_at_utc DESC;
            """,
            new { InvestigationId = investigationId },
            cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task SaveQueryRunAsync(InvestigationQueryRunRecord run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT OR IGNORE INTO investigation_query_runs (id, investigation_id, pivot_id, query_text, started_at_utc, duration_ms, succeeded, row_count, diagnostics_json, result_schema_json)
            VALUES (@Id, @InvestigationId, @PivotId, @QueryText, @StartedAtUtc, @DurationMs, @Succeeded, @RowCount, @DiagnosticsJson, @ResultSchemaJson);
            """,
            new { run.Id, run.InvestigationId, run.PivotId, run.QueryText, StartedAtUtc = Format(run.StartedAtUtc), run.DurationMs, Succeeded = run.Succeeded ? 1 : 0, run.RowCount, run.DiagnosticsJson, run.ResultSchemaJson },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<InvestigationQueryRunRecord>> ListQueryRunsAsync(string investigationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(investigationId);

        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<QueryRunRow>(new CommandDefinition(
            """
            SELECT id AS Id, investigation_id AS InvestigationId, pivot_id AS PivotId, query_text AS QueryText,
                started_at_utc AS StartedAtUtc, duration_ms AS DurationMs, succeeded AS Succeeded,
                row_count AS RowCount, diagnostics_json AS DiagnosticsJson, result_schema_json AS ResultSchemaJson
            FROM investigation_query_runs
            WHERE investigation_id = @InvestigationId
            ORDER BY started_at_utc DESC;
            """,
            new { InvestigationId = investigationId },
            cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task SaveEvidenceAsync(IReadOnlyList<EvidenceRecord> evidence, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        foreach (var item in evidence)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT OR IGNORE INTO investigation_evidence (id, investigation_id, query_run_id, source_table, source_reference_json, row_snapshot_json, summary, created_by, created_at_utc)
                VALUES (@Id, @InvestigationId, @QueryRunId, @SourceTable, @SourceReferenceJson, @RowSnapshotJson, @Summary, @CreatedBy, @CreatedAtUtc);
                """,
                new { item.Id, item.InvestigationId, item.QueryRunId, item.SourceTable, item.SourceReferenceJson, item.RowSnapshotJson, item.Summary, item.CreatedBy, CreatedAtUtc = Format(item.CreatedAtUtc) },
                cancellationToken: cancellationToken));
        }
    }

    public async Task<IReadOnlyList<EvidenceRecord>> ListEvidenceAsync(string investigationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(investigationId);

        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<EvidenceRow>(new CommandDefinition(
            """
            SELECT id AS Id, investigation_id AS InvestigationId, query_run_id AS QueryRunId, source_table AS SourceTable,
                source_reference_json AS SourceReferenceJson, row_snapshot_json AS RowSnapshotJson, summary AS Summary,
                created_by AS CreatedBy, created_at_utc AS CreatedAtUtc
            FROM investigation_evidence
            WHERE investigation_id = @InvestigationId
            ORDER BY created_at_utc ASC;
            """,
            new { InvestigationId = investigationId },
            cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task AddTagsAsync(IReadOnlyList<EvidenceTagRecord> tags, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tags);

        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        foreach (var tag in tags)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT OR IGNORE INTO investigation_evidence_tags (evidence_id, tag, added_by, added_at_utc)
                VALUES (@EvidenceId, @Tag, @AddedBy, @AddedAtUtc);
                """,
                new { tag.EvidenceId, tag.Tag, tag.AddedBy, AddedAtUtc = Format(tag.AddedAtUtc) },
                cancellationToken: cancellationToken));
        }
    }

    public async Task<IReadOnlyList<EvidenceTagRecord>> ListTagsAsync(string investigationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(investigationId);

        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<TagRow>(new CommandDefinition(
            """
            SELECT t.evidence_id AS EvidenceId, t.tag AS Tag, t.added_by AS AddedBy, t.added_at_utc AS AddedAtUtc
            FROM investigation_evidence_tags t
            INNER JOIN investigation_evidence e ON e.id = t.evidence_id
            WHERE e.investigation_id = @InvestigationId
            ORDER BY t.added_at_utc ASC;
            """,
            new { InvestigationId = investigationId },
            cancellationToken: cancellationToken));

        return rows.Select(r => new EvidenceTagRecord(r.EvidenceId, r.Tag, r.AddedBy, Parse(r.AddedAtUtc))).ToArray();
    }

    public async Task AddCommentAsync(EvidenceCommentRecord comment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comment);

        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT OR IGNORE INTO investigation_evidence_comments (id, evidence_id, body, created_by, created_at_utc)
            VALUES (@Id, @EvidenceId, @Body, @CreatedBy, @CreatedAtUtc);
            """,
            new { comment.Id, comment.EvidenceId, comment.Body, comment.CreatedBy, CreatedAtUtc = Format(comment.CreatedAtUtc) },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<EvidenceCommentRecord>> ListCommentsAsync(string investigationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(investigationId);

        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<CommentRow>(new CommandDefinition(
            """
            SELECT c.id AS Id, c.evidence_id AS EvidenceId, c.body AS Body, c.created_by AS CreatedBy, c.created_at_utc AS CreatedAtUtc
            FROM investigation_evidence_comments c
            INNER JOIN investigation_evidence e ON e.id = c.evidence_id
            WHERE e.investigation_id = @InvestigationId
            ORDER BY c.created_at_utc ASC;
            """,
            new { InvestigationId = investigationId },
            cancellationToken: cancellationToken));

        return rows.Select(r => new EvidenceCommentRecord(r.Id, r.EvidenceId, r.Body, r.CreatedBy, Parse(r.CreatedAtUtc))).ToArray();
    }

    public async Task AddEvidenceLinkAsync(EvidenceLinkRecord link, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(link);

        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT OR IGNORE INTO investigation_evidence_links (id, investigation_id, from_evidence_id, to_evidence_id, relationship, created_by, created_at_utc)
            VALUES (@Id, @InvestigationId, @FromEvidenceId, @ToEvidenceId, @Relationship, @CreatedBy, @CreatedAtUtc);
            """,
            new { link.Id, link.InvestigationId, link.FromEvidenceId, link.ToEvidenceId, link.Relationship, link.CreatedBy, CreatedAtUtc = Format(link.CreatedAtUtc) },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<EvidenceLinkRecord>> ListEvidenceLinksAsync(string investigationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(investigationId);

        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LinkRow>(new CommandDefinition(
            """
            SELECT id AS Id, investigation_id AS InvestigationId, from_evidence_id AS FromEvidenceId,
                to_evidence_id AS ToEvidenceId, relationship AS Relationship, created_by AS CreatedBy, created_at_utc AS CreatedAtUtc
            FROM investigation_evidence_links
            WHERE investigation_id = @InvestigationId
            ORDER BY created_at_utc ASC;
            """,
            new { InvestigationId = investigationId },
            cancellationToken: cancellationToken));

        return rows.Select(r => new EvidenceLinkRecord(r.Id, r.InvestigationId, r.FromEvidenceId, r.ToEvidenceId, r.Relationship, r.CreatedBy, Parse(r.CreatedAtUtc))).ToArray();
    }

    public async Task AddEntityLinkAsync(EvidenceEntityLinkRecord link, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(link);

        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT OR IGNORE INTO investigation_evidence_entity_links (id, evidence_id, entity_kind, entity_key, entity_display, created_by, created_at_utc)
            VALUES (@Id, @EvidenceId, @EntityKind, @EntityKey, @EntityDisplay, @CreatedBy, @CreatedAtUtc);
            """,
            new { link.Id, link.EvidenceId, EntityKind = link.EntityKind.ToString(), link.EntityKey, link.EntityDisplay, link.CreatedBy, CreatedAtUtc = Format(link.CreatedAtUtc) },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<EvidenceEntityLinkRecord>> ListEntityLinksAsync(string investigationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(investigationId);

        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<EntityLinkRow>(new CommandDefinition(
            """
            SELECT l.id AS Id, l.evidence_id AS EvidenceId, l.entity_kind AS EntityKind, l.entity_key AS EntityKey,
                l.entity_display AS EntityDisplay, l.created_by AS CreatedBy, l.created_at_utc AS CreatedAtUtc
            FROM investigation_evidence_entity_links l
            INNER JOIN investigation_evidence e ON e.id = l.evidence_id
            WHERE e.investigation_id = @InvestigationId
            ORDER BY l.created_at_utc ASC;
            """,
            new { InvestigationId = investigationId },
            cancellationToken: cancellationToken));

        return rows.Select(r => new EvidenceEntityLinkRecord(
            r.Id,
            r.EvidenceId,
            Enum.Parse<InvestigationEntityKind>(r.EntityKind),
            r.EntityKey,
            r.EntityDisplay,
            r.CreatedBy,
            Parse(r.CreatedAtUtc))).ToArray();
    }

    private static InvestigationRecord ToRecord(InvestigationRow row)
        => new(row.Id, row.Title, row.Description, row.Status, row.CreatedBy, Parse(row.CreatedAtUtc), Parse(row.UpdatedAtUtc));

    private static InvestigationPivotRecord ToRecord(PivotRow row)
        => new(row.Id, row.InvestigationId, row.Name, row.QueryText, row.Description, Parse(row.CreatedAtUtc), Parse(row.UpdatedAtUtc));

    private static InvestigationQueryRunRecord ToRecord(QueryRunRow row)
        => new(row.Id, row.InvestigationId, row.PivotId, row.QueryText, Parse(row.StartedAtUtc), row.DurationMs, row.Succeeded != 0, row.RowCount, row.DiagnosticsJson, row.ResultSchemaJson);

    private static EvidenceRecord ToRecord(EvidenceRow row)
        => new(row.Id, row.InvestigationId, row.QueryRunId, row.SourceTable, row.SourceReferenceJson, row.RowSnapshotJson, row.Summary, row.CreatedBy, Parse(row.CreatedAtUtc));

    private sealed class InvestigationRow
    {
        public string Id { get; init; } = "";
        public string Title { get; init; } = "";
        public string? Description { get; init; }
        public string Status { get; init; } = "";
        public string CreatedBy { get; init; } = "";
        public string CreatedAtUtc { get; init; } = "";
        public string UpdatedAtUtc { get; init; } = "";
    }

    private sealed class PivotRow
    {
        public string Id { get; init; } = "";
        public string InvestigationId { get; init; } = "";
        public string Name { get; init; } = "";
        public string QueryText { get; init; } = "";
        public string? Description { get; init; }
        public string CreatedAtUtc { get; init; } = "";
        public string UpdatedAtUtc { get; init; } = "";
    }

    private sealed class QueryRunRow
    {
        public string Id { get; init; } = "";
        public string InvestigationId { get; init; } = "";
        public string PivotId { get; init; } = "";
        public string QueryText { get; init; } = "";
        public string StartedAtUtc { get; init; } = "";
        public long DurationMs { get; init; }
        public int Succeeded { get; init; }
        public int? RowCount { get; init; }
        public string? DiagnosticsJson { get; init; }
        public string? ResultSchemaJson { get; init; }
    }

    private sealed class EvidenceRow
    {
        public string Id { get; init; } = "";
        public string InvestigationId { get; init; } = "";
        public string? QueryRunId { get; init; }
        public string? SourceTable { get; init; }
        public string? SourceReferenceJson { get; init; }
        public string RowSnapshotJson { get; init; } = "";
        public string? Summary { get; init; }
        public string CreatedBy { get; init; } = "";
        public string CreatedAtUtc { get; init; } = "";
    }

    private sealed class TagRow
    {
        public string EvidenceId { get; init; } = "";
        public string Tag { get; init; } = "";
        public string AddedBy { get; init; } = "";
        public string AddedAtUtc { get; init; } = "";
    }

    private sealed class CommentRow
    {
        public string Id { get; init; } = "";
        public string EvidenceId { get; init; } = "";
        public string Body { get; init; } = "";
        public string CreatedBy { get; init; } = "";
        public string CreatedAtUtc { get; init; } = "";
    }

    private sealed class LinkRow
    {
        public string Id { get; init; } = "";
        public string InvestigationId { get; init; } = "";
        public string FromEvidenceId { get; init; } = "";
        public string ToEvidenceId { get; init; } = "";
        public string Relationship { get; init; } = "";
        public string CreatedBy { get; init; } = "";
        public string CreatedAtUtc { get; init; } = "";
    }

    private sealed class EntityLinkRow
    {
        public string Id { get; init; } = "";
        public string EvidenceId { get; init; } = "";
        public string EntityKind { get; init; } = "";
        public string EntityKey { get; init; } = "";
        public string? EntityDisplay { get; init; }
        public string CreatedBy { get; init; } = "";
        public string CreatedAtUtc { get; init; } = "";
    }
}
