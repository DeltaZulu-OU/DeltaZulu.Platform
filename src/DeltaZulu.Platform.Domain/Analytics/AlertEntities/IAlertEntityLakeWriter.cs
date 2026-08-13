namespace DeltaZulu.Platform.Domain.Analytics.AlertEntities;

/// <summary>Appends immutable extracted alert entities to the analytics lake.</summary>
public interface IAlertEntityLakeWriter
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    Task AppendBatchAsync(IReadOnlyList<AlertEntityRecord> entities, CancellationToken cancellationToken = default);
}
