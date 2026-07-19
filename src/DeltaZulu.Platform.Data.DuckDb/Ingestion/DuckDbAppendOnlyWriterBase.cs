using System.Globalization;
using System.Text;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Data.DuckDb.Ingestion;

/// <summary>
/// Shared append/batch-append plumbing for the internal DuckDB observation writers: a single
/// writer gate, and the row-building primitives (timestamp/string escaping and NULL handling)
/// that every append-only writer needs regardless of its own column shape.
/// </summary>
public abstract class DuckDbAppendOnlyWriterBase<TSnapshot> : IDisposable
{
    private readonly SchemaApplier _applier;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    protected DuckDbAppendOnlyWriterBase(SchemaApplier applier)
    {
        ArgumentNullException.ThrowIfNull(applier);
        _applier = applier;
    }

    public async Task AppendAsync(TSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            _applier.ExecuteRaw(BuildInsertSql(snapshot));
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task AppendBatchAsync(IReadOnlyList<TSnapshot> snapshots, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        if (snapshots.Count == 0) return;

        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            _applier.ExecuteRaw(BuildBatchInsertSql(snapshots));
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public void Dispose() => _writeGate.Dispose();

    protected abstract string BuildInsertSql(TSnapshot snapshot);

    protected abstract string BuildBatchInsertSql(IReadOnlyList<TSnapshot> snapshots);

    protected static void AppendTimestamp(StringBuilder sb, DateTime utc)
    {
        sb.Append("TIMESTAMP '");
        sb.Append(FormatTimestamp(utc));
        sb.Append('\'');
    }

    protected static void AppendNullableTimestamp(StringBuilder sb, DateTime? utc)
    {
        if (utc is null)
        {
            sb.Append("NULL");
            return;
        }

        AppendTimestamp(sb, utc.Value);
    }

    protected static void AppendString(StringBuilder sb, string value)
    {
        sb.Append('\'');
        sb.Append(SqlLiteralEscaping.EscapeSingleQuotes(value));
        sb.Append('\'');
    }

    protected static void AppendNullableString(StringBuilder sb, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            sb.Append("NULL");
            return;
        }

        AppendString(sb, value);
    }

    protected static string FormatTimestamp(DateTime utc) =>
        utc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
