using Microsoft.Data.Sqlite;

namespace Hunting.Web.Services;

public sealed class UserSettingsStore
{
    public const string DefaultTimeFilterKey = "none";

    private const string CreateSchemaSql =
        """
        CREATE TABLE IF NOT EXISTS user_settings (
            id INTEGER PRIMARY KEY CHECK (id = 1),
            default_time_filter TEXT NOT NULL,
            default_result_limit INTEGER NULL
        );

        INSERT INTO user_settings (id, default_time_filter, default_result_limit)
        VALUES (1, 'none', NULL)
        ON CONFLICT(id) DO NOTHING;
        """;

    private const string SelectSql =
        "SELECT default_time_filter, default_result_limit FROM user_settings WHERE id = 1";

    private const string UpdateSql =
        """
        UPDATE user_settings
        SET default_time_filter = $timeFilter,
            default_result_limit = $resultLimit
        WHERE id = 1;
        """;

    private readonly string _connectionString;

    public UserSettingsStore(IWebHostEnvironment environment)
    {
        var dbPath = Path.Combine(environment.ContentRootPath, "settings.db");
        _connectionString = $"Data Source={dbPath}";
    }

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await ExecuteNonQueryAsync(connection, CreateSchemaSql, cancellationToken);
    }

    public async Task<(string DefaultTimeFilter, int? DefaultResultLimit)> LoadAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = SelectSql;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return (DefaultTimeFilterKey, null);
        }

        var defaultTimeFilter = reader.GetString(0);
        int? defaultResultLimit = reader.IsDBNull(1) ? null : reader.GetInt32(1);
        return (defaultTimeFilter, defaultResultLimit);
    }

    public async Task SaveAsync(string defaultTimeFilter, int? defaultResultLimit, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = UpdateSql;
        command.Parameters.AddWithValue("$timeFilter", defaultTimeFilter);
        command.Parameters.AddWithValue("$resultLimit", defaultResultLimit is null ? DBNull.Value : defaultResultLimit.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}