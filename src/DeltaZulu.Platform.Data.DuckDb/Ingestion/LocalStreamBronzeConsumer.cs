using DeltaZulu.LocalStream;
using DeltaZulu.Platform.Ingestion.PubSub;

namespace DeltaZulu.Platform.Data.DuckDb.Ingestion;

/// <summary>
/// Drains a durable raw-log subscription into the Bronze subscriber. Input
/// offsets are committed only after the corresponding lake write succeeds.
/// </summary>
/// <remarks>
/// LocalStream has at-least-once delivery semantics. The transactional writer
/// deduplicates replayed stream occurrences; the generic subscriber overload
/// does not guarantee idempotent persistence. Neither deduplicates source events.
/// </remarks>
public sealed class LocalStreamBronzeConsumer
{
    private readonly LocalStreamHost _host;
    private readonly Func<StreamRecord<RawLogEnvelope>, CancellationToken, ValueTask> _write;
    private readonly string _topic;
    private readonly string _subscriptionId;

    public LocalStreamBronzeConsumer(
        LocalStreamHost host,
        IRawLogSubscriber lake,
        string topic,
        string subscriptionId)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        ArgumentNullException.ThrowIfNull(lake);
        _write = (entry, ct) => lake.HandleAsync(new RawLogBatch(entry.EventId, entry.Payload.Channel, [entry.Payload]), ct);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
        _topic = topic;
        _subscriptionId = subscriptionId;
    }

    public LocalStreamBronzeConsumer(LocalStreamHost host, LocalStreamBronzeWriter writer, string topic, string subscriptionId)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        ArgumentNullException.ThrowIfNull(writer);
        _write = writer.WriteAsync;
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
            await _write(entry, cancellationToken).ConfigureAwait(false);
            await consumer.CommitAsync(entry.Position, cancellationToken).ConfigureAwait(false);
            count++;
        }

        return count;
    }
}
