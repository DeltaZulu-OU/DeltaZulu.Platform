namespace Hunting.Data;

using DuckDB.NET.Data;

/// <summary>
/// Manages DuckDB connection lifecycle. For MVP, provides a single
/// in-memory or file-backed connection for the Blazor Server backend.
///
/// THREAD SAFETY: The lock in GetConnection() guards only the lazy-open race
/// condition. DuckDB itself, when embedded in-process, does not support fully
/// concurrent multi-threaded access on a single connection. This factory is
/// designed for single-user Blazor Server use. Concurrent query execution from
/// multiple Blazor circuits will serialize at the DuckDB level but may still
/// produce interleaved results on the same connection object.
///
/// Post-MVP: Quack protocol migration (targeted DuckDB v2.0, September 2026)
/// will replace this with per-request connections and server-side authorization.
/// </summary>
public sealed class DuckDbConnectionFactory : IDisposable
{
    private readonly string _connectionString;
    private readonly IReadOnlyList<string> _startupSql;
    private readonly Lock _lock = new();
    private DuckDBConnection? _connection;

    public DuckDbConnectionFactory(
        string connectionString = "DataSource=:memory:",
        IReadOnlyList<string>? startupSql = null)
    {
        _connectionString = connectionString;
        _startupSql = startupSql ?? [
            "LOAD inet"];
    }

    /// <summary>
    /// Returns the shared connection, opening it lazily if necessary.
    /// Thread-safe for the open race; not designed for concurrent query execution.
    /// </summary>
    public DuckDBConnection GetConnection()
    {
        lock (_lock)
        {
            if (_connection is null)
            {
                var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                try
                {
                    ApplyStartupSql(connection, _startupSql);
                    _connection = connection;
                }
                catch
                {
                    connection.Dispose();
                    throw;
                }
            }
            return _connection;
        }
    }

    private static void ApplyStartupSql(DuckDBConnection connection, IReadOnlyList<string> startupSql)
    {
        if (startupSql.Count == 0)
        {
            return;
        }

        using var cmd = connection.CreateCommand();
        foreach (var sql in startupSql)
        {
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }
}
