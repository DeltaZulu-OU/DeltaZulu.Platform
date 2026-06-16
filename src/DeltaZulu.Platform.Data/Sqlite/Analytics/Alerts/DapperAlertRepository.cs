using Dapper;
using DeltaZulu.Platform.Domain.Analytics.Alerts;
using static DeltaZulu.Platform.Data.Sqlite.Analytics.SqliteDateTimeHelpers;

namespace DeltaZulu.Platform.Data.Sqlite.Analytics.Alerts;

public sealed class DapperAlertRepository : DapperRepositoryBase, IAlertRepository
{
    private const string CreateSchemaSql =
        """
        CREATE TABLE IF NOT EXISTS alerts (
            id TEXT PRIMARY KEY,
            detection_id TEXT NOT NULL,
            detection_version INTEGER NOT NULL,
            detection_run_id TEXT NOT NULL,
            alert_time_utc TEXT NOT NULL,
            source_view TEXT NOT NULL,
            source_event_id TEXT NULL,
            severity TEXT NOT NULL,
            confidence TEXT NOT NULL,
            risk_score INTEGER NOT NULL DEFAULT 0,
            evidence_json TEXT NOT NULL,
            status TEXT NOT NULL DEFAULT 'New',
            created_at_utc TEXT NOT NULL,
            updated_at_utc TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_alerts_detection_run_id
            ON alerts (detection_run_id, alert_time_utc ASC);

        CREATE INDEX IF NOT EXISTS idx_alerts_detection_id
            ON alerts (detection_id, alert_time_utc DESC);

        CREATE INDEX IF NOT EXISTS idx_alerts_status
            ON alerts (status, alert_time_utc DESC);

        CREATE INDEX IF NOT EXISTS idx_alerts_source_event
            ON alerts (detection_id, source_event_id);
        """;

    private const string ListByRunSql =
        """
        SELECT
            id AS Id,
            detection_id AS DetectionId,
            detection_version AS DetectionVersion,
            detection_run_id AS DetectionRunId,
            alert_time_utc AS AlertTimeUtc,
            source_view AS SourceView,
            source_event_id AS SourceEventId,
            severity AS Severity,
            confidence AS Confidence,
            risk_score AS RiskScore,
            evidence_json AS EvidenceJson,
            status AS Status,
            created_at_utc AS CreatedAtUtc,
            updated_at_utc AS UpdatedAtUtc
        FROM alerts
        WHERE detection_run_id = @DetectionRunId
        ORDER BY alert_time_utc ASC;
        """;

    private const string ListByDetectionSql =
        """
        SELECT
            id AS Id,
            detection_id AS DetectionId,
            detection_version AS DetectionVersion,
            detection_run_id AS DetectionRunId,
            alert_time_utc AS AlertTimeUtc,
            source_view AS SourceView,
            source_event_id AS SourceEventId,
            severity AS Severity,
            confidence AS Confidence,
            risk_score AS RiskScore,
            evidence_json AS EvidenceJson,
            status AS Status,
            created_at_utc AS CreatedAtUtc,
            updated_at_utc AS UpdatedAtUtc
        FROM alerts
        WHERE detection_id = @DetectionId
        ORDER BY alert_time_utc DESC;
        """;

    private const string GetSql =
        """
        SELECT
            id AS Id,
            detection_id AS DetectionId,
            detection_version AS DetectionVersion,
            detection_run_id AS DetectionRunId,
            alert_time_utc AS AlertTimeUtc,
            source_view AS SourceView,
            source_event_id AS SourceEventId,
            severity AS Severity,
            confidence AS Confidence,
            risk_score AS RiskScore,
            evidence_json AS EvidenceJson,
            status AS Status,
            created_at_utc AS CreatedAtUtc,
            updated_at_utc AS UpdatedAtUtc
        FROM alerts
        WHERE id = @Id;
        """;

    private const string UpsertSql =
        """
        INSERT INTO alerts (
            id, detection_id, detection_version, detection_run_id,
            alert_time_utc, source_view, source_event_id,
            severity, confidence, risk_score, evidence_json,
            status, created_at_utc, updated_at_utc
        )
        VALUES (
            @Id, @DetectionId, @DetectionVersion, @DetectionRunId,
            @AlertTimeUtc, @SourceView, @SourceEventId,
            @Severity, @Confidence, @RiskScore, @EvidenceJson,
            @Status, @CreatedAtUtc, @UpdatedAtUtc
        )
        ON CONFLICT(id) DO UPDATE SET
            status = excluded.status,
            updated_at_utc = excluded.updated_at_utc;
        """;

    private const string UpdateStatusSql =
        """
        UPDATE alerts
        SET status = @Status,
            updated_at_utc = @UpdatedAtUtc
        WHERE id = @Id;
        """;

    public DapperAlertRepository(IAppDbConnectionFactory connectionFactory)
        : base(connectionFactory, CreateSchemaSql)
    {
    }

    public async Task<IReadOnlyList<AlertRecord>> ListByRunAsync(string detectionRunId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detectionRunId);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<AlertRow>(
            new CommandDefinition(ListByRunSql, new { DetectionRunId = detectionRunId }, cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task<IReadOnlyList<AlertRecord>> ListByDetectionAsync(string detectionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detectionId);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<AlertRow>(
            new CommandDefinition(ListByDetectionSql, new { DetectionId = detectionId }, cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task<AlertRecord?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<AlertRow>(
            new CommandDefinition(GetSql, new { Id = id }, cancellationToken: cancellationToken));

        return row is null ? null : ToRecord(row);
    }

    public async Task SaveAsync(AlertRecord alert, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alert);
        ArgumentException.ThrowIfNullOrWhiteSpace(alert.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(alert.DetectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(alert.DetectionRunId);
        ArgumentException.ThrowIfNullOrWhiteSpace(alert.SourceView);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        await ExecuteUpsertAsync(connection, alert, cancellationToken);
    }

    public async Task SaveBatchAsync(IReadOnlyList<AlertRecord> alerts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alerts);

        if (alerts.Count == 0)
        {
            return;
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var alert in alerts)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(alert.Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(alert.DetectionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(alert.DetectionRunId);
            ArgumentException.ThrowIfNullOrWhiteSpace(alert.SourceView);

            await ExecuteUpsertAsync(connection, alert, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(string id, string status, DateTime updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            UpdateStatusSql,
            new {
                Id = id,
                Status = status,
                UpdatedAtUtc = Format(updatedAtUtc)
            },
            cancellationToken: cancellationToken));
    }

    private static async Task ExecuteUpsertAsync(
        System.Data.Common.DbConnection connection,
        AlertRecord alert,
        CancellationToken cancellationToken) => await connection.ExecuteAsync(new CommandDefinition(
            UpsertSql,
            new {
                alert.Id,
                alert.DetectionId,
                alert.DetectionVersion,
                alert.DetectionRunId,
                AlertTimeUtc = Format(alert.AlertTimeUtc),
                alert.SourceView,
                alert.SourceEventId,
                alert.Severity,
                alert.Confidence,
                alert.RiskScore,
                alert.EvidenceJson,
                alert.Status,
                CreatedAtUtc = Format(alert.CreatedAtUtc),
                UpdatedAtUtc = Format(alert.UpdatedAtUtc)
            },
            cancellationToken: cancellationToken));

    private static AlertRecord ToRecord(AlertRow row) => new AlertRecord(
            row.Id,
            row.DetectionId,
            row.DetectionVersion,
            row.DetectionRunId,
            Parse(row.AlertTimeUtc),
            row.SourceView,
            row.SourceEventId,
            row.Severity,
            row.Confidence,
            row.RiskScore,
            row.EvidenceJson,
            row.Status,
            Parse(row.CreatedAtUtc),
            Parse(row.UpdatedAtUtc));

    private sealed class AlertRow
    {
        public string Id { get; init; } = string.Empty;
        public string DetectionId { get; init; } = string.Empty;
        public int DetectionVersion { get; init; }
        public string DetectionRunId { get; init; } = string.Empty;
        public string AlertTimeUtc { get; init; } = string.Empty;
        public string SourceView { get; init; } = string.Empty;
        public string? SourceEventId { get; init; }
        public string Severity { get; init; } = string.Empty;
        public string Confidence { get; init; } = string.Empty;
        public int RiskScore { get; init; }
        public string EvidenceJson { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string CreatedAtUtc { get; init; } = string.Empty;
        public string UpdatedAtUtc { get; init; } = string.Empty;
    }
}