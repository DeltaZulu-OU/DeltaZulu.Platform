using DeltaZulu.LocalStream;

namespace DeltaZulu.Platform.Ingestion.PubSub;

/// <summary>
/// Persists normalized raw-log batches to a LocalStream topic. This is an append
/// boundary, not evidence persistence: the lake consumer independently checkpoints
/// only after successful durable Bronze writes.
/// </summary>
public sealed class LocalStreamRawLogPublisher : IRawLogPublisher
{
    private readonly ILocalStreamProducer<RawLogEnvelope> _producer;
    private readonly string _topic;

    public LocalStreamRawLogPublisher(LocalStreamHost host, string topic)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        _producer = host.CreateProducer<RawLogEnvelope>();
        _topic = topic;
    }

    public async ValueTask PublishAsync(RawLogBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();

        // Keep each normalized event independently addressable for replay.
        // LocalStream generates unique event IDs unless the source has an approved
        // stable identity; batch IDs alone cannot serve as event identities.
        foreach (var item in batch.Events)
        {
            var result = await _producer.AppendAsync(
                _topic,
                item.Normalize(),
                new AppendOptions { PartitionKey = item.Channel },
                cancellationToken).ConfigureAwait(false);

            if (result.Status != AppendStatus.Appended)
            {
                throw new InvalidOperationException(
                    $"LocalStream append failed for channel '{item.Channel}' with status '{result.Status}': {result.Reason}");
            }
        }
    }
}
