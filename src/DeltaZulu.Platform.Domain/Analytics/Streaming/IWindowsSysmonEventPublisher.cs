namespace DeltaZulu.Platform.Domain.Analytics.Streaming;

/// <summary>
/// Publishes raw Windows Sysmon events to the <c>bronze.windows_sysmon_event</c> Proton stream.
/// </summary>
public interface IWindowsSysmonEventPublisher
{
    Task PublishAsync(BronzeRawEntry entry, CancellationToken ct = default);
    Task PublishBatchAsync(IEnumerable<BronzeRawEntry> entries, CancellationToken ct = default);
}
