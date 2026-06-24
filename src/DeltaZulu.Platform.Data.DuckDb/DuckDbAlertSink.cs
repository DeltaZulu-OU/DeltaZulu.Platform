using Dapper;
using DeltaZulu.Platform.Domain.Analytics.Alerts;

namespace DeltaZulu.Platform.Data.DuckDb;

public sealed class DuckDbAlertSink : IAlertSink
{
    private const string CreateSchemaSql =
        """
        CREATE TABLE IF NOT EXISTS alerts (
            id                  TEXT PRIMARY KEY,
            detection_id        TEXT NOT NULL,
            detection_version   INTEGER NOT NULL,
            detection_run_id    TEXT NOT NULL,
            alert_time_utc      TIMESTAMP NOT NULL,
            source_view         TEXT NOT NULL,
            source_event_id     TEXT NULL,
            severity            TEXT NOT NULL,
            confidence          TEXT NOT NULL,
            risk_score          INTEGER NOT NULL,
            evidence_json       TEXT NOT NULL,
            status              TEXT NOT NULL DEFAULT 'New',
            created_at_utc      TIMESTAMP NOT NULL,
            updated_at_utc      TIMESTAMP NOT NULL
        );
        """;

    private const string InsertSql =
        """
        INSERT INTO alerts (
            id, detection_id, detection_version, detection_run_id,
            alert_time_utc, source_view, source_event_id,
            severity, confidence, risk_score, evidence_json,
            status, created_at_utc, updated_at_utc
        ) VALUES (
            @Id, @DetectionId, @DetectionVersion, @DetectionRunId,
            @AlertTimeUtc, @SourceView, @SourceEventId,
            @Severity, @Confidence, @RiskScore, @EvidenceJson,
            @Status, @CreatedAtUtc, @UpdatedAtUtc
        )
        ON CONFLICT(id) DO NOTHING;
        """;

    private readonly DuckDbConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _initialized;

    public DuckDbAlertSink(DuckDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task WriteAsync(AlertRecord alert, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(alert);
        await EnsureInitializedAsync(ct);
        await Task.Run(() => _connectionFactory.GetConnection().Execute(InsertSql, ToParams(alert)), ct);
    }

    public async Task WriteBatchAsync(IReadOnlyList<AlertRecord> alerts, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(alerts);
        if (alerts.Count == 0) return;
        await EnsureInitializedAsync(ct);
        await Task.Run(() =>
        {
            var conn = _connectionFactory.GetConnection();
            using var tx = conn.BeginTransaction();
            foreach (var alert in alerts)
                conn.Execute(InsertSql, ToParams(alert), tx);
            tx.Commit();
        }, ct);
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized) return;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            await Task.Run(() => _connectionFactory.GetConnection().Execute(CreateSchemaSql), ct);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static object ToParams(AlertRecord a) => new
    {
        a.Id,
        a.DetectionId,
        a.DetectionVersion,
        a.DetectionRunId,
        a.AlertTimeUtc,
        a.SourceView,
        a.SourceEventId,
        a.Severity,
        a.Confidence,
        a.RiskScore,
        a.EvidenceJson,
        a.Status,
        a.CreatedAtUtc,
        a.UpdatedAtUtc
    };
}
