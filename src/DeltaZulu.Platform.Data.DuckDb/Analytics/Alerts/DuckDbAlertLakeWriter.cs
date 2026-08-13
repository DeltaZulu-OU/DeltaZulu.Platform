using Dapper;
using DeltaZulu.Platform.Domain.Analytics.Alerts;

namespace DeltaZulu.Platform.Data.DuckDb.Analytics.Alerts;

/// <summary>DuckDB-backed append-only writer for alert evidence.</summary>
public sealed class DuckDbAlertLakeWriter(DuckDbConnectionFactory connectionFactory) : IAlertLakeWriter
{
    private const string CreateSchemaSql = """
        CREATE SCHEMA IF NOT EXISTS lake;
        CREATE TABLE IF NOT EXISTS lake.alert_events (
            id VARCHAR NOT NULL, detection_id VARCHAR NOT NULL, detection_version INTEGER NOT NULL,
            detection_run_id VARCHAR NOT NULL, alert_time_utc TIMESTAMP NOT NULL, source_view VARCHAR NOT NULL,
            source_event_id VARCHAR, severity VARCHAR NOT NULL, confidence VARCHAR NOT NULL,
            risk_score INTEGER NOT NULL, evidence_json JSON NOT NULL, created_at_utc TIMESTAMP NOT NULL
        );
        """;

    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        connectionFactory.GetConnection().Execute(CreateSchemaSql);
        return Task.CompletedTask;
    }

    public async Task AppendAsync(AlertRecord alert, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alert);
        await AppendBatchAsync([alert], cancellationToken);
    }

    public async Task AppendBatchAsync(IReadOnlyList<AlertRecord> alerts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alerts);
        if (alerts.Count == 0) return;
        await EnsureInitializedAsync(cancellationToken);
        var connection = connectionFactory.GetConnection();
        foreach (var alert in alerts)
        {
            ArgumentNullException.ThrowIfNull(alert);
            cancellationToken.ThrowIfCancellationRequested();
            connection.Execute(BuildInsertSql(alert));
        }
    }

    private static string BuildInsertSql(AlertRecord alert) => $"""
        INSERT INTO lake.alert_events VALUES (
            {StringLiteral(alert.Id)}, {StringLiteral(alert.DetectionId)}, {alert.DetectionVersion},
            {StringLiteral(alert.DetectionRunId)}, {TimestampLiteral(alert.AlertTimeUtc)}, {StringLiteral(alert.SourceView)},
            {NullableStringLiteral(alert.SourceEventId)}, {StringLiteral(alert.Severity)}, {StringLiteral(alert.Confidence)},
            {alert.RiskScore}, CAST({StringLiteral(alert.EvidenceJson)} AS JSON), {TimestampLiteral(alert.CreatedAtUtc)});
        """;

    private static string StringLiteral(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private static string NullableStringLiteral(string? value) => value is null ? "NULL" : StringLiteral(value);

    private static string TimestampLiteral(DateTime value) => StringLiteral(value.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
}
