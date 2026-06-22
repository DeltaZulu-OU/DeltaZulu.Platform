namespace DeltaZulu.Platform.Ingestion.PubSub;

/// <summary>
/// A publish unit for one raw-log channel. Batches provide the pub-sub boundary
/// between collectors/seeders and consumers such as DuckDB lake tables or Proton.
/// </summary>
public sealed class RawLogBatch
{
    public RawLogBatch(string batchId, string channel, IEnumerable<RawLogEnvelope> events)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(batchId);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentNullException.ThrowIfNull(events);

        var normalized = events.Select(static item => item.Normalize()).ToArray();
        if (normalized.Any(item => !item.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("All raw log events in a batch must target the batch channel.", nameof(events));
        }

        BatchId = batchId.Trim();
        Channel = channel.Trim();
        Events = normalized;
    }

    public string BatchId { get; }
    public string Channel { get; }
    public IReadOnlyList<RawLogEnvelope> Events { get; }
    public int Count => Events.Count;
}
