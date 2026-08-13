using System.Text;
using Dapper;
using DeltaZulu.Platform.Ingestion.PubSub;

namespace DeltaZulu.Platform.Data.DuckDb.Ingestion;

/// <summary>
/// Consumes raw-log pub-sub batches into DuckDB Bronze tables. The channel-to-table
/// map is supplied by composition so Proton, lake, and other consumers can use
/// the same published raw-log stream without depending on DuckDB.
/// </summary>
public sealed class DuckDbRawLogSubscriber : IRawLogSubscriber, IDisposable
{
    private const int DefaultMaxRowsPerInsert = 1_000;
    private readonly SchemaApplier _applier;
    private readonly int _maxRowsPerInsert;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly IReadOnlyDictionary<string, string> _tableByChannel;

    public DuckDbRawLogSubscriber(
        SchemaApplier applier,
        IReadOnlyDictionary<string, string> tableByChannel,
        int maxRowsPerInsert = DefaultMaxRowsPerInsert)
    {
        ArgumentNullException.ThrowIfNull(applier);
        ArgumentNullException.ThrowIfNull(tableByChannel);
        if (maxRowsPerInsert <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRowsPerInsert), maxRowsPerInsert, "Insert chunks must contain at least one row.");
        }

        _applier = applier;
        _maxRowsPerInsert = maxRowsPerInsert;
        _tableByChannel = new Dictionary<string, string>(tableByChannel, StringComparer.OrdinalIgnoreCase);
    }

    public async ValueTask HandleAsync(RawLogBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();

        if (batch.Count == 0)
        {
            return;
        }

        if (!_tableByChannel.TryGetValue(batch.Channel, out var tableName))
        {
            throw new InvalidOperationException($"No DuckDB Bronze table route is registered for raw-log channel '{batch.Channel}'.");
        }

        ValidateQualifiedTableName(tableName);

        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            for (var offset = 0; offset < batch.Events.Count; offset += _maxRowsPerInsert)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = Math.Min(_maxRowsPerInsert, batch.Events.Count - offset);
                var (sql, parameters) = BuildInsert(tableName, batch.Events, offset, count);
                _applier.ExecuteParameterized(sql, parameters);
            }
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public void Dispose() => _writeGate.Dispose();

    /// <summary>
    /// Builds a parameterized multi-row INSERT. Raw log content is untrusted
    /// (agent- and ultimately source-controlled), so every value is bound as a
    /// query parameter rather than interpolated into SQL text.
    /// </summary>
    private static (string Sql, DynamicParameters Parameters) BuildInsert(
        string tableName,
        IReadOnlyList<RawLogEnvelope> events,
        int offset,
        int count)
    {
        var builder = new StringBuilder(capacity: Math.Min(1_048_576, 256 + (count * 128)));
        var parameters = new DynamicParameters();
        var paramIndex = 0;

        builder.Append("INSERT INTO ");
        builder.Append(tableName);
        builder.AppendLine(" (ingest_time, source_name, provider, host, raw_log, raw_text) VALUES");

        for (var i = 0; i < count; i++)
        {
            var item = events[offset + i].Normalize();
            if (i > 0)
            {
                builder.AppendLine(",");
            }

            var ingestTimeParam = "p" + paramIndex++;
            var sourceNameParam = "p" + paramIndex++;
            var providerParam = "p" + paramIndex++;
            var hostParam = "p" + paramIndex++;
            var rawLogParam = "p" + paramIndex++;
            var rawTextParam = "p" + paramIndex++;

            // DuckDB's parameter marker is "$name", not "@name" - "@" is its unary
            // abs() operator, so an "@name" placeholder parses as an expression
            // instead of a bind parameter.
            builder.Append('(')
                .Append('$').Append(ingestTimeParam).Append(", $").Append(sourceNameParam)
                .Append(", $").Append(providerParam).Append(", $").Append(hostParam)
                .Append(", CAST($").Append(rawLogParam).Append(" AS JSON), $").Append(rawTextParam)
                .Append(')');

            parameters.Add(ingestTimeParam, item.IngestTimeUtc.UtcDateTime);
            parameters.Add(sourceNameParam, item.SourceName);
            parameters.Add(providerParam, item.Provider);
            parameters.Add(hostParam, item.Host);
            parameters.Add(rawLogParam, item.RawLog);
            parameters.Add(rawTextParam, item.RawText);
        }

        builder.Append(';');
        return (builder.ToString(), parameters);
    }

    private static void ValidateQualifiedTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName) || tableName.Any(static ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '.')))
        {
            throw new InvalidOperationException($"Unsafe DuckDB Bronze table route '{tableName}'.");
        }
    }

}
