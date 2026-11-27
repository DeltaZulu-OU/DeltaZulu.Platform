using Dapper;
using DeltaZulu.LocalStream;
using DeltaZulu.Platform.Ingestion.PubSub;

namespace DeltaZulu.Platform.Data.DuckDb.Ingestion;

/// <summary>Persists stream occurrences and their receipts in one lake transaction.
/// This is replay deduplication, not source-event deduplication.
/// Requires a dedicated connection and a single writer instance.</summary>
public sealed class LocalStreamBronzeWriter : IDisposable
{
    private readonly DuckDbConnectionFactory _factory;
    private readonly IReadOnlyDictionary<string, string> _routes;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LocalStreamBronzeWriter(DuckDbConnectionFactory factory, IReadOnlyDictionary<string, string> routes)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _routes = new Dictionary<string, string>(routes, StringComparer.OrdinalIgnoreCase);
    }

    public async ValueTask WriteAsync(StreamRecord<RawLogEnvelope> entry, CancellationToken cancellationToken = default)
    {
        var item = entry.Payload.Normalize();
        if (!_routes.TryGetValue(item.Channel, out var table) ||
            string.IsNullOrWhiteSpace(table) || table.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch is '_' or '.')))
            throw new InvalidOperationException($"Invalid Bronze route for '{item.Channel}'.");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var connection = _factory.GetConnection();
            using var transaction = connection.BeginTransaction();
            connection.Execute("CREATE SCHEMA IF NOT EXISTS internal", transaction: transaction);
            connection.Execute("""
                CREATE TABLE IF NOT EXISTS internal.bronze_stream_receipts (
                    topic VARCHAR NOT NULL, partition_id INTEGER NOT NULL,
                    stream_offset BIGINT NOT NULL, event_id VARCHAR NOT NULL,
                    PRIMARY KEY (topic, partition_id, stream_offset, event_id))
                """, transaction: transaction);
            var key = new { topic = entry.Topic, partition_id = entry.Partition, stream_offset = entry.Offset, event_id = entry.EventId };
            if (connection.ExecuteScalar<long>("SELECT count(*) FROM internal.bronze_stream_receipts WHERE topic=$topic AND partition_id=$partition_id AND stream_offset=$stream_offset AND event_id=$event_id", key, transaction) == 0)
            {
                connection.Execute($"INSERT INTO {table} (ingest_time, source_name, provider, host, raw_log, raw_text) VALUES ($ingest, $source, $provider, $host, CAST($raw AS JSON), $text)",
                    new { ingest = item.IngestTimeUtc.UtcDateTime, source = item.SourceName, provider = item.Provider, host = item.Host, raw = item.RawLog, text = item.RawText }, transaction);
                connection.Execute("INSERT INTO internal.bronze_stream_receipts VALUES ($topic, $partition_id, $stream_offset, $event_id)", key, transaction);
            }
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
