namespace DeltaZulu.Hunting.Data;

using Dapper;
using DuckDB.NET.Data;

/// <summary>
/// <para>
/// Manages DuckDB connection lifecycle. For MVP, provides a single
/// in-memory or file-backed connection for the Blazor Server backend.
/// </para>
/// <para>
/// THREAD SAFETY: The lock in GetConnection() guards only the lazy-open race
/// condition. DuckDB itself, when embedded in-process, does not support fully
/// concurrent multi-threaded access on a single connection. This factory is
/// designed for single-user Blazor Server use. Concurrent query execution from
/// multiple Blazor circuits must serialize above this shared connection.
/// </para>
/// <para>
/// Direct DuckDB.NET ownership is intentionally limited to connection lifecycle.
/// SQL execution that does not require dynamic readers should go through Dapper.
/// Dynamic KQL execution remains DuckDB.NET-based because it needs streaming,
/// column metadata, custom extension types, and provider-specific value handling.
/// </para>
/// </summary>
public sealed class DuckDbConnectionFactory : IDisposable
{
    private readonly string _connectionString;
    private readonly Lock _lock = new();
    private readonly IReadOnlyList<string> _startupSql;
    private DuckDBConnection? _connection;

    public DuckDbConnectionFactory(
        string connectionString = "DataSource=:memory:",
        IReadOnlyList<string>? startupSql = null)
    {
        _connectionString = connectionString;
        _startupSql = startupSql ?? [
            "INSTALL inet;LOAD inet;"];
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _connection?.Dispose();
            _connection = null;
        }
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
        foreach (var sql in startupSql.Where(static sql => !string.IsNullOrWhiteSpace(sql)))
        {
            connection.Execute(sql);
        }
    }
}
