using System.Text;
using Dapper;

namespace DeltaZulu.Platform.Data.DuckDb.Ingestion;

/// <summary>
/// Shared append/batch-append plumbing for the internal DuckDB observation writers: a single
/// writer gate, and a parameterized row-building primitive that every append-only writer needs
/// regardless of its own column shape. Values are always bound as query parameters, never
/// interpolated into SQL text, since these rows carry agent-reported (untrusted) content.
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
            var (sql, parameters) = BuildInsert(snapshot);
            _applier.ExecuteParameterized(sql, parameters);
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
            var (sql, parameters) = BuildBatchInsert(snapshots);
            _applier.ExecuteParameterized(sql, parameters);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public void Dispose() => _writeGate.Dispose();

    protected abstract (string Sql, DynamicParameters Parameters) BuildInsert(TSnapshot snapshot);

    protected abstract (string Sql, DynamicParameters Parameters) BuildBatchInsert(IReadOnlyList<TSnapshot> snapshots);

    /// <summary>
    /// Appends one row's "($pN, $pN+1, ...)" placeholder group to the VALUES clause
    /// and binds each value into <paramref name="parameters"/> under that name.
    /// DuckDB's parameter marker is "$name", not "@name" - "@" is its unary abs()
    /// operator, so an "@name" placeholder parses as an expression instead of a
    /// bind parameter. <paramref name="nextParamIndex"/> is shared across every row
    /// in a batch so placeholder names stay unique across the whole statement.
    /// </summary>
    protected static void AppendRowPlaceholders(
        StringBuilder sql, DynamicParameters parameters, ref int nextParamIndex, params object?[] values)
    {
        sql.Append('(');
        for (var i = 0; i < values.Length; i++)
        {
            if (i > 0) sql.Append(", ");
            var name = "p" + nextParamIndex++;
            sql.Append('$').Append(name);
            parameters.Add(name, values[i]);
        }
        sql.Append(')');
    }

    /// <summary>Empty-string source fields mean "not reported"; store as NULL, matching prior behavior.</summary>
    protected static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
