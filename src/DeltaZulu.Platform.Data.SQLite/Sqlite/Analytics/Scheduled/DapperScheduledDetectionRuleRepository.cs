using Dapper;
using DeltaZulu.Platform.Domain.Analytics.Scheduled;
using static DeltaZulu.Platform.Data.Sqlite.Analytics.SqliteDateTimeHelpers;

namespace DeltaZulu.Platform.Data.Sqlite.Analytics.Scheduled;

public sealed class DapperScheduledDetectionRuleRepository : DapperRepositoryBase, IScheduledDetectionRuleRepository
{
    private const string CreateSchemaSql =
        """
        CREATE TABLE IF NOT EXISTS scheduled_rules (
            id                   TEXT PRIMARY KEY,
            title                TEXT NOT NULL,
            description          TEXT NULL,
            kql_query            TEXT NOT NULL,
            proton_select_sql    TEXT NULL,
            scheduled_task_ddl   TEXT NULL,
            schedule_seconds     INTEGER NOT NULL DEFAULT 3600,
            lookback_seconds     INTEGER NOT NULL DEFAULT 3600,
            target_stream        TEXT NOT NULL DEFAULT 'alert_dispatch',
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

        CREATE INDEX IF NOT EXISTS idx_scheduled_rules_enabled
            ON scheduled_rules (is_enabled, updated_at_utc DESC);
        """;

    private const string SelectColumns =
        """
        id                   AS Id,
        title                AS Title,
        description          AS Description,
        kql_query            AS KqlQuery,
        proton_select_sql    AS ProtonSelectSql,
        scheduled_task_ddl   AS ScheduledTaskDdl,
        schedule_seconds     AS ScheduleSeconds,
        lookback_seconds     AS LookbackSeconds,
        target_stream        AS TargetStream,
        threshold            AS Threshold,
        severity             AS Severity,
        confidence           AS Confidence,
        risk_score           AS RiskScore,
        mitre_tactics        AS MitreTactics,
        mitre_techniques     AS MitreTechniques,
        is_enabled           AS IsEnabled,
        created_at_utc       AS CreatedAtUtc,
        updated_at_utc       AS UpdatedAtUtc
        """;

    private const string ListSql = $"SELECT {SelectColumns} FROM scheduled_rules ORDER BY updated_at_utc DESC;";
    private const string GetSql  = $"SELECT {SelectColumns} FROM scheduled_rules WHERE id = @Id;";

    private const string UpsertSql =
        """
        INSERT INTO scheduled_rules (
            id, title, description, kql_query, proton_select_sql, scheduled_task_ddl,
            schedule_seconds, lookback_seconds, target_stream,
            threshold, severity, confidence, risk_score,
            mitre_tactics, mitre_techniques, is_enabled,
            created_at_utc, updated_at_utc
        ) VALUES (
            @Id, @Title, @Description, @KqlQuery, @ProtonSelectSql, @ScheduledTaskDdl,
            @ScheduleSeconds, @LookbackSeconds, @TargetStream,
            @Threshold, @Severity, @Confidence, @RiskScore,
            @MitreTactics, @MitreTechniques, @IsEnabled,
            @CreatedAtUtc, @UpdatedAtUtc
        )
        ON CONFLICT(id) DO UPDATE SET
            title              = excluded.title,
            description        = excluded.description,
            kql_query          = excluded.kql_query,
            proton_select_sql  = excluded.proton_select_sql,
            scheduled_task_ddl = excluded.scheduled_task_ddl,
            schedule_seconds   = excluded.schedule_seconds,
            lookback_seconds   = excluded.lookback_seconds,
            target_stream      = excluded.target_stream,
            threshold          = excluded.threshold,
            severity           = excluded.severity,
            confidence         = excluded.confidence,
            risk_score         = excluded.risk_score,
            mitre_tactics      = excluded.mitre_tactics,
            mitre_techniques   = excluded.mitre_techniques,
            is_enabled         = excluded.is_enabled,
            updated_at_utc     = excluded.updated_at_utc;
        """;

    private const string DeleteSql = "DELETE FROM scheduled_rules WHERE id = @Id;";

    public DapperScheduledDetectionRuleRepository(IAppDbConnectionFactory connectionFactory)
        : base(connectionFactory, CreateSchemaSql) { }

    public async Task<IReadOnlyList<ScheduledDetectionRule>> ListAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        await using var conn = await ConnectionFactory.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<Row>(new CommandDefinition(ListSql, cancellationToken: ct));
        return rows.Select(ToRecord).ToArray();
    }

    public async Task<ScheduledDetectionRule?> GetAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await EnsureInitializedAsync(ct);
        await using var conn = await ConnectionFactory.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            new CommandDefinition(GetSql, new { Id = id }, cancellationToken: ct));
        return row is null ? null : ToRecord(row);
    }

    public async Task SaveAsync(ScheduledDetectionRule rule, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        await EnsureInitializedAsync(ct);
        await using var conn = await ConnectionFactory.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(UpsertSql, new {
            rule.Id,
            rule.Title,
            rule.Description,
            rule.KqlQuery,
            rule.ProtonSelectSql,
            rule.ScheduledTaskDdl,
            ScheduleSeconds  = (int)rule.Schedule.TotalSeconds,
            LookbackSeconds  = (int)rule.Lookback.TotalSeconds,
            rule.TargetStream,
            rule.Threshold,
            rule.Severity,
            rule.Confidence,
            rule.RiskScore,
            rule.MitreTactics,
            rule.MitreTechniques,
            IsEnabled        = rule.IsEnabled ? 1 : 0,
            CreatedAtUtc     = Format(rule.CreatedAtUtc),
            UpdatedAtUtc     = Format(rule.UpdatedAtUtc)
        }, cancellationToken: ct));
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await EnsureInitializedAsync(ct);
        await using var conn = await ConnectionFactory.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(DeleteSql, new { Id = id }, cancellationToken: ct));
    }

    private static ScheduledDetectionRule ToRecord(Row r) => new(
        r.Id,
        r.Title,
        r.Description,
        r.KqlQuery,
        r.ProtonSelectSql,
        r.ScheduledTaskDdl,
        TimeSpan.FromSeconds(r.ScheduleSeconds),
        TimeSpan.FromSeconds(r.LookbackSeconds),
        r.TargetStream,
        r.Threshold,
        r.Severity,
        r.Confidence,
        r.RiskScore,
        r.MitreTactics,
        r.MitreTechniques,
        r.IsEnabled != 0,
        Parse(r.CreatedAtUtc),
        Parse(r.UpdatedAtUtc));

    private sealed class Row
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string KqlQuery { get; init; } = string.Empty;
        public string? ProtonSelectSql { get; init; }
        public string? ScheduledTaskDdl { get; init; }
        public int ScheduleSeconds { get; init; }
        public int LookbackSeconds { get; init; }
        public string TargetStream { get; init; } = "alert_dispatch";
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
