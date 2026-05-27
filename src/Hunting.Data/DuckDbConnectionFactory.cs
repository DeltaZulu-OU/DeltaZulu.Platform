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
    private readonly Lock _lock = new();
    private DuckDBConnection? _connection;

    public DuckDbConnectionFactory(string connectionString = "DataSource=:memory:")
    {
        _connectionString = connectionString;
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
                _connection = new DuckDBConnection(_connectionString);
                _connection.Open();
            }
            return _connection;
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
