using Dapper;
using DeltaZulu.Platform.Domain.Analytics.Nrt;
using static DeltaZulu.Platform.Data.Sqlite.Analytics.SqliteDateTimeHelpers;

namespace DeltaZulu.Platform.Data.Sqlite.Analytics.Nrt;

public sealed class DapperNrtRuleRepository : DapperRepositoryBase, INrtRuleRepository
{
    private const string CreateSchemaSql =
        """
        CREATE TABLE IF NOT EXISTS nrt_rules (
            id                   TEXT PRIMARY KEY,
            title                TEXT NOT NULL,
            description          TEXT NULL,
            kql_query            TEXT NOT NULL,
            proton_select_sql    TEXT NULL,
            materialized_view_ddl TEXT NULL,
            threshold            INTEGER NOT NULL DEFAULT 0,
            severity             TEXT NOT NULL DEFAULT 'Medium',
            confidence           TEXT NOT NULL DEFAULT 'Medium',
            risk_score           INTEGER NOT NULL DEFAULT 50,
            mitre_tactics        TEXT NULL,
            mitre_techniques     TEXT NULL,
            is_enabled           INTEGER NOT NULL DEFAULT 1,
            created_at_utc       TEXT NOT NULL,
            updated_at_utc       TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_nrt_rules_enabled
            ON nrt_rules (is_enabled, updated_at_utc DESC);
        """;

    private const string SelectColumns =
        """
        id                    AS Id,
        title                 AS Title,
        description           AS Description,
        kql_query             AS KqlQuery,
        proton_select_sql     AS ProtonSelectSql,
        materialized_view_ddl AS MaterializedViewDdl,
        threshold             AS Threshold,
        severity              AS Severity,
        confidence            AS Confidence,
        risk_score            AS RiskScore,
        mitre_tactics         AS MitreTactics,
        mitre_techniques      AS MitreTechniques,
        is_enabled            AS IsEnabled,
        created_at_utc        AS CreatedAtUtc,
        updated_at_utc        AS UpdatedAtUtc
        """;

    private const string ListSql = $"SELECT {SelectColumns} FROM nrt_rules ORDER BY updated_at_utc DESC;";

    private const string GetSql = $"SELECT {SelectColumns} FROM nrt_rules WHERE id = @Id;";

    private const string UpsertSql =
        """
        INSERT INTO nrt_rules (
            id, title, description, kql_query, proton_select_sql, materialized_view_ddl,
            threshold, severity, confidence, risk_score,
            mitre_tactics, mitre_techniques, is_enabled,
            created_at_utc, updated_at_utc
        ) VALUES (
            @Id, @Title, @Description, @KqlQuery, @ProtonSelectSql, @MaterializedViewDdl,
            @Threshold, @Severity, @Confidence, @RiskScore,
            @MitreTactics, @MitreTechniques, @IsEnabled,
            @CreatedAtUtc, @UpdatedAtUtc
        )
        ON CONFLICT(id) DO UPDATE SET
            title                 = excluded.title,
            description           = excluded.description,
            kql_query             = excluded.kql_query,
            proton_select_sql     = excluded.proton_select_sql,
            materialized_view_ddl = excluded.materialized_view_ddl,
            threshold             = excluded.threshold,
            severity              = excluded.severity,
            confidence            = excluded.confidence,
            risk_score            = excluded.risk_score,
            mitre_tactics         = excluded.mitre_tactics,
            mitre_techniques      = excluded.mitre_techniques,
            is_enabled            = excluded.is_enabled,
            updated_at_utc        = excluded.updated_at_utc;
        """;

    private const string DeleteSql = "DELETE FROM nrt_rules WHERE id = @Id;";

    public DapperNrtRuleRepository(IAppDbConnectionFactory connectionFactory)
        : base(connectionFactory, CreateSchemaSql)
    {
    }

    public async Task<IReadOnlyList<NrtRule>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<NrtRuleRow>(
            new CommandDefinition(ListSql, cancellationToken: cancellationToken));
        return rows.Select(ToRecord).ToArray();
    }

    public async Task<NrtRule?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<NrtRuleRow>(
            new CommandDefinition(GetSql, new { Id = id }, cancellationToken: cancellationToken));
        return row is null ? null : ToRecord(row);
    }

    public async Task SaveAsync(NrtRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(UpsertSql, new {
            rule.Id,
            rule.Title,
            rule.Description,
            rule.KqlQuery,
            rule.ProtonSelectSql,
            rule.MaterializedViewDdl,
            rule.Threshold,
            rule.Severity,
            rule.Confidence,
            rule.RiskScore,
            rule.MitreTactics,
            rule.MitreTechniques,
            IsEnabled     = rule.IsEnabled ? 1 : 0,
            CreatedAtUtc  = Format(rule.CreatedAtUtc),
            UpdatedAtUtc  = Format(rule.UpdatedAtUtc)
        }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(DeleteSql, new { Id = id }, cancellationToken: cancellationToken));
    }

    private static NrtRule ToRecord(NrtRuleRow r) => new(
        r.Id,
        r.Title,
        r.Description,
        r.KqlQuery,
        r.ProtonSelectSql,
        r.MaterializedViewDdl,
        r.Threshold,
        r.Severity,
        r.Confidence,
        r.RiskScore,
        r.MitreTactics,
        r.MitreTechniques,
        r.IsEnabled != 0,
        Parse(r.CreatedAtUtc),
        Parse(r.UpdatedAtUtc));

    private sealed class NrtRuleRow
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string KqlQuery { get; init; } = string.Empty;
        public string? ProtonSelectSql { get; init; }
        public string? MaterializedViewDdl { get; init; }
        public int Threshold { get; init; }
        public string Severity { get; init; } = "Medium";
        public string Confidence { get; init; } = "Medium";
        public int RiskScore { get; init; }
        public string? MitreTactics { get; init; }
        public string? MitreTechniques { get; init; }
        public int IsEnabled { get; init; }
        public string CreatedAtUtc { get; init; } = string.Empty;
        public string UpdatedAtUtc { get; init; } = string.Empty;
    }
}
