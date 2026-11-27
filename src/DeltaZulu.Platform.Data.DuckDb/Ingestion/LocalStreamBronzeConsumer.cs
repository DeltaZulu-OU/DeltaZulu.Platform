using DeltaZulu.LocalStream;
using DeltaZulu.Platform.Ingestion.PubSub;

namespace DeltaZulu.Platform.Data.DuckDb.Ingestion;

/// <summary>
/// Drains a durable raw-log subscription into the Bronze subscriber. Input
/// offsets are committed only after the corresponding lake write succeeds.
/// </summary>
/// <remarks>
/// LocalStream has at-least-once semantics: a crash after lake insertion but
/// before the checkpoint can replay a row. Until an atomic source-identity
/// deduplication contract is implemented, this consumer does not claim exactly
/// once Bronze persistence.
/// </remarks>
public sealed class LocalStreamBronzeConsumer
{
    private readonly LocalStreamHost _host;
    private readonly IRawLogSubscriber _lake;
    private readonly string _topic;
    private readonly string _subscriptionId;

    public LocalStreamBronzeConsumer(
        LocalStreamHost host,
        IRawLogSubscriber lake,
        string topic,
        string subscriptionId)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _lake = lake ?? throw new ArgumentNullException(nameof(lake));
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
        _topic = topic;
        _subscriptionId = subscriptionId;
    }

    public async Task<int> DrainOnceAsync(CancellationToken cancellationToken = default)
    {
        var consumer = _host.CreateConsumer<RawLogEnvelope>(_subscriptionId);
        var count = 0;

        // Let OffsetExpiredException propagate. Silently skipping expired
        // evidence would violate the required Bronze evidence path.
        await foreach (var entry in consumer.ReadAsync(_topic, cancellationToken: cancellationToken))
        {
            var payload = entry.Payload.Normalize();
            await _lake.HandleAsync(
                new RawLogBatch(entry.EventId, payload.Channel, [payload]),
                cancellationToken).ConfigureAwait(false);
            await consumer.CommitAsync(entry.Position, cancellationToken).ConfigureAwait(false);
            count++;
        }

        return count;
    }
}
