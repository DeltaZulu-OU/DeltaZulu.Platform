using Dapper;
using DeltaZulu.Platform.Domain.Analytics.Alerts;

namespace DeltaZulu.Platform.Data.DuckDb;

public sealed class DuckDbAlertSink : IAlertSink, IDisposable
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
            created_at_utc      TIMESTAMP NOT NULL
        );
        """;

    private const string InsertSql =
        """
        INSERT INTO alerts (
            id, detection_id, detection_version, detection_run_id,
            alert_time_utc, source_view, source_event_id,
            severity, confidence, risk_score, evidence_json,
            created_at_utc
        ) VALUES (
            @Id, @DetectionId, @DetectionVersion, @DetectionRunId,
            @AlertTimeUtc, @SourceView, @SourceEventId,
            @Severity, @Confidence, @RiskScore, @EvidenceJson,
            @CreatedAtUtc
        )
        ON CONFLICT(id) DO NOTHING;
        """;

    private readonly DuckDbConnectionFactory _connectionFactory;
    // DuckDbConnectionFactory is not designed for concurrent query execution (see its XML doc).
    // A single semaphore serializes all writes (init + insert) through this sink.
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _initialized;

    public DuckDbAlertSink(DuckDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task WriteAsync(AlertRecord alert, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(alert);
        await _lock.WaitAsync(ct);
        try
        {
            EnsureInitialized();
            _connectionFactory.GetConnection().Execute(InsertSql, ToParams(alert));
        }
        finally { _lock.Release(); }
    }

    public async Task WriteBatchAsync(IReadOnlyList<AlertRecord> alerts, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(alerts);
        if (alerts.Count == 0)
        {
            return;
        }

        await _lock.WaitAsync(ct);
        try
        {
            EnsureInitialized();
            var conn = _connectionFactory.GetConnection();
            await using var tx = conn.BeginTransaction();
            try
            {
                foreach (var alert in alerts)
                {
                    conn.Execute(InsertSql, ToParams(alert), tx);
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
        finally { _lock.Release(); }
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        _connectionFactory.GetConnection().Execute(CreateSchemaSql);
        _initialized = true;
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
        a.CreatedAtUtc
    };
    public void Dispose() => _lock.Dispose();
}
