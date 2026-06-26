namespace DeltaZulu.Platform.Domain.Analytics.Alerts;

public interface IAlertRepository
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertRecord>> ListByRunAsync(string detectionRunId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertRecord>> ListByDetectionAsync(string detectionId, CancellationToken cancellationToken = default);

    Task<AlertRecord?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task SaveAsync(AlertRecord alert, CancellationToken cancellationToken = default);

    Task SaveBatchAsync(IReadOnlyList<AlertRecord> alerts, CancellationToken cancellationToken = default);
}