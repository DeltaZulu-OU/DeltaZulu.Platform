using Dapper;
using DuckDB.NET.Data;

namespace DeltaZulu.Platform.Data.DuckDb;
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
    private readonly IReadOnlyList<DuckDbAttachedDatabase> _attachedDatabases;
    private readonly IReadOnlyList<string> _startupSql;
    private DuckDBConnection? _connection;

    public DuckDbConnectionFactory(
        string connectionString = "DataSource=:memory:",
        IReadOnlyList<string>? startupSql = null,
        IReadOnlyList<DuckDbAttachedDatabase>? attachedDatabases = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        _connectionString = connectionString;
        _startupSql = startupSql ?? [
            "INSTALL inet;LOAD inet;"];
        _attachedDatabases = attachedDatabases ?? [];

        ValidateAttachedDatabases(_attachedDatabases);
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

                try
                {
                    connection.Open();
                    ApplyStartupSql(connection, _startupSql);
                    AttachDatabases(connection, _attachedDatabases);
                    _connection = connection;
                }
                catch (Exception ex)
                {
                    connection.Dispose();
                    throw new InvalidOperationException("DuckDB connection initialization failed. See the inner exception for the failed startup step.", ex);
                }
            }

            return _connection;
        }
    }

    private static void ApplyStartupSql(DuckDBConnection connection, IReadOnlyList<string> startupSql)
    {
        for (var index = 0; index < startupSql.Count; index++)
        {
            var sql = startupSql[index];
            if (string.IsNullOrWhiteSpace(sql))
            {
                continue;
            }

            try
            {
                connection.Execute(sql);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"DuckDB startup SQL statement at index {index} failed.", ex);
            }
        }
    }

    private static void AttachDatabases(
        DuckDBConnection connection,
        IReadOnlyList<DuckDbAttachedDatabase> attachedDatabases)
    {
        if (attachedDatabases.Count == 0)
        {
            return;
        }

        if (attachedDatabases.Any(static database => database.Type.Equals("sqlite", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                connection.Execute("INSTALL sqlite;LOAD sqlite;");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to load DuckDB's SQLite extension required by attached SQLite databases.", ex);
            }
        }

        foreach (var database in attachedDatabases)
        {
            try
            {
                connection.Execute(BuildAttachSql(database));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to attach DuckDB database '{database.Alias}' with type '{database.Type}'.",
                    ex);
            }

            CreateAttachedViews(connection, database);
        }
    }

    internal static string BuildAttachSql(DuckDbAttachedDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        var alias = QuoteIdentifier(database.Alias);
        var path = QuoteStringLiteral(database.Path);
        var type = FormatDatabaseType(database.Type);
        var readOnly = database.ReadOnly ? ", READ_ONLY" : string.Empty;
        return $"ATTACH {path} AS {alias} (TYPE {type}{readOnly});";
    }

    private static void CreateAttachedViews(DuckDBConnection connection, DuckDbAttachedDatabase database)
    {
        foreach (var view in database.Views)
        {
            try
            {
                connection.Execute(BuildCreateViewSql(database, view));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to create DuckDB view '{view.TargetSchema}.{view.Name}' for attached database '{database.Alias}' table '{view.SourceSchema}.{view.SourceTable}'.",
                    ex);
            }
        }
    }

    internal static string BuildCreateViewSql(DuckDbAttachedDatabase database, DuckDbAttachedView view)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(view);

        var targetSchema = QuoteIdentifier(view.TargetSchema);
        var targetView = QuoteIdentifier(view.Name);
        var sourceAlias = QuoteIdentifier(database.Alias);
        var sourceSchema = QuoteIdentifier(view.SourceSchema);
        var sourceTable = QuoteIdentifier(view.SourceTable);

        return $"CREATE SCHEMA IF NOT EXISTS {targetSchema};" + Environment.NewLine
            + $"CREATE OR REPLACE VIEW {targetSchema}.{targetView} AS "
            + $"SELECT * FROM {sourceAlias}.{sourceSchema}.{sourceTable};";
    }

    private static void ValidateAttachedDatabases(IReadOnlyList<DuckDbAttachedDatabase> attachedDatabases)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var viewNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var database in attachedDatabases)
        {
            ArgumentNullException.ThrowIfNull(database);
            if (!aliases.Add(database.Alias))
            {
                throw new ArgumentException($"Duplicate attached DuckDB database alias '{database.Alias}'.", nameof(attachedDatabases));
            }

            foreach (var view in database.Views)
            {
                ArgumentNullException.ThrowIfNull(view);
                var viewKey = $"{view.TargetSchema}.{view.Name}";
                if (!viewNames.Add(viewKey))
                {
                    throw new ArgumentException($"Duplicate attached DuckDB view target '{viewKey}'.", nameof(attachedDatabases));
                }
            }
        }
    }

    private static string FormatDatabaseType(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        if (type.Any(static character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            throw new ArgumentException("Database type may only contain ASCII letters, digits, and underscores.", nameof(type));
        }

        return type;
    }

    internal static string QuoteIdentifier(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    internal static string QuoteStringLiteral(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }
}
