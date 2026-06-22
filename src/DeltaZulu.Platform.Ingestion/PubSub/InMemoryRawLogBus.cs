namespace DeltaZulu.Platform.Ingestion.PubSub;

/// <summary>
/// In-process raw-log pub-sub bus. This is intentionally infrastructure-light so
/// tests, development seeders, and future collectors can share the same boundary
/// before a durable broker is introduced.
/// </summary>
public sealed class InMemoryRawLogBus : IRawLogPublisher
{
    private readonly object _gate = new();
    private readonly Dictionary<string, IRawLogSubscriber[]> _subscribers = new(StringComparer.OrdinalIgnoreCase);

    public void Subscribe(string channel, IRawLogSubscriber subscriber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentNullException.ThrowIfNull(subscriber);

        lock (_gate)
        {
            var normalizedChannel = channel.Trim();
            if (!_subscribers.TryGetValue(normalizedChannel, out var existing))
            {
                _subscribers[normalizedChannel] = [subscriber];
                return;
            }

            if (existing.Contains(subscriber))
            {
                return;
            }

            var updated = new IRawLogSubscriber[existing.Length + 1];
            Array.Copy(existing, updated, existing.Length);
            updated[^1] = subscriber;
            _subscribers[normalizedChannel] = updated;
        }
    }

    public async ValueTask PublishAsync(RawLogBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        IRawLogSubscriber[] subscribers;
        lock (_gate)
        {
            subscribers = _subscribers.TryGetValue(batch.Channel, out var registered)
                ? registered
                : [];
        }

        if (subscribers.Length == 0)
        {
            return;
        }

        if (subscribers.Length == 1)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await subscribers[0].HandleAsync(batch, cancellationToken);
            return;
        }

        var tasks = new Task[subscribers.Length];
        for (var i = 0; i < subscribers.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            tasks[i] = subscribers[i].HandleAsync(batch, cancellationToken).AsTask();
        }

        await Task.WhenAll(tasks);
    }
}
