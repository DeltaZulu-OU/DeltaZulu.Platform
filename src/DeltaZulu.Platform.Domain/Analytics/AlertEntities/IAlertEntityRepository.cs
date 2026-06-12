namespace DeltaZulu.Platform.Domain.Analytics.AlertEntities;

public interface IAlertEntityRepository
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertEntityRecord>> ListByAlertAsync(string alertId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertEntityRecord>> ListByEntityValueAsync(string entityType, string entityValue, CancellationToken cancellationToken = default);

    Task SaveBatchAsync(IReadOnlyList<AlertEntityRecord> entities, CancellationToken cancellationToken = default);
}
