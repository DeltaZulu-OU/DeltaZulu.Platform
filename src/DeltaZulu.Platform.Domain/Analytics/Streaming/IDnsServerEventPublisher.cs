namespace DeltaZulu.Platform.Domain.Analytics.Streaming;

/// <summary>
/// Publishes raw DNS server events to the <c>bronze.dns_server_event</c> Proton stream.
/// </summary>
public interface IDnsServerEventPublisher
{
    Task PublishAsync(BronzeRawEntry entry, CancellationToken ct = default);
    Task PublishBatchAsync(IEnumerable<BronzeRawEntry> entries, CancellationToken ct = default);
}
