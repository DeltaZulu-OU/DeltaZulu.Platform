using System.Globalization;
using System.Text;
using DeltaZulu.Platform.Domain.Common;
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
                _applier.ExecuteRaw(BuildInsertSql(tableName, batch.Events, offset, count));
            }
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public void Dispose() => _writeGate.Dispose();

    private static string BuildInsertSql(
        string tableName,
        IReadOnlyList<RawLogEnvelope> events,
        int offset,
        int count)
    {
        var builder = new StringBuilder(capacity: Math.Min(1_048_576, 256 + (count * 512)));
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

            builder.Append("(TIMESTAMP '");
            builder.Append(item.IngestTimeUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            builder.Append("', '");
            builder.Append(SqlLiteralEscaping.EscapeSingleQuotes(item.SourceName));
            builder.Append("', '");
            builder.Append(SqlLiteralEscaping.EscapeSingleQuotes(item.Provider));
            builder.Append("', '");
            builder.Append(SqlLiteralEscaping.EscapeSingleQuotes(item.Host));
            builder.Append("', CAST('");
            builder.Append(SqlLiteralEscaping.EscapeSingleQuotes(item.RawLog));
            builder.Append("' AS JSON), '");
            builder.Append(SqlLiteralEscaping.EscapeSingleQuotes(item.RawText));
            builder.Append("')");
        }

        builder.Append(';');
        return builder.ToString();
    }

    private static void ValidateQualifiedTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName) || tableName.Any(static ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '.')))
        {
            throw new InvalidOperationException($"Unsafe DuckDB Bronze table route '{tableName}'.");
        }
    }

}
