namespace DeltaZulu.Platform.Domain.Analytics.Alerts;

/// <summary>Appends immutable alert evidence to the analytics lake.</summary>
public interface IAlertLakeWriter
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    Task AppendAsync(AlertRecord alert, CancellationToken cancellationToken = default);

    Task AppendBatchAsync(IReadOnlyList<AlertRecord> alerts, CancellationToken cancellationToken = default);
}
