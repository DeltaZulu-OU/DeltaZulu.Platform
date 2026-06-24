namespace DeltaZulu.Platform.Domain.Analytics.Alerts;

/// <summary>
/// Write-only port for appending detection alerts to the DuckDB data lake.
/// Kept separate from <see cref="IAlertRepository"/> (CRUD for investigation) because
/// the mediation daemon writes and analysts read through different consumers.
/// </summary>
public interface IAlertSink
{
    Task WriteAsync(AlertRecord alert, CancellationToken ct = default);
    Task WriteBatchAsync(IReadOnlyList<AlertRecord> alerts, CancellationToken ct = default);
}
