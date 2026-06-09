namespace Hunting.Data.SavedQueries;

using Dapper;
using Hunting.Data.Persistence;
using AppISavedQueryRepository = Hunting.Application.SavedQueries.ISavedQueryRepository;
using AppSavedQueryRecord = Hunting.Application.SavedQueries.SavedQueryRecord;

public sealed class DapperSavedQueryRepository : AppISavedQueryRepository, IDisposable
{
    private const string CreateSchemaSql =
        """
        CREATE TABLE IF NOT EXISTS saved_queries (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            description TEXT NULL,
            query_text TEXT NOT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL,
            last_run_at TEXT NULL
        );
        """;

    private const string ListSql =
        """
        SELECT
            id AS Id,
            name AS Name,
            description AS Description,
            query_text AS QueryText,
            created_at AS CreatedAt,
            updated_at AS UpdatedAt,
            last_run_at AS LastRunAt
        FROM saved_queries
        ORDER BY updated_at DESC, name ASC;
        """;

    private const string GetSql =
        """
        SELECT
            id AS Id,
            name AS Name,
            description AS Description,
            query_text AS QueryText,
            created_at AS CreatedAt,
            updated_at AS UpdatedAt,
            last_run_at AS LastRunAt
        FROM saved_queries
        WHERE id = @Id;
        """;

    private const string UpsertSql =
        """
        INSERT INTO saved_queries (
            id,
            name,
            description,
            query_text,
            created_at,
            updated_at,
            last_run_at
        )
        VALUES (
            @Id,
            @Name,
            @Description,
            @QueryText,
            @CreatedAt,
            @UpdatedAt,
            @LastRunAt
        )
        ON CONFLICT(id) DO UPDATE SET
            name = excluded.name,
            description = excluded.description,
            query_text = excluded.query_text,
            updated_at = excluded.updated_at,
            last_run_at = excluded.last_run_at;
        """;

    private const string DeleteSql =
        """
        DELETE FROM saved_queries
        WHERE id = @Id;
        """;

    private const string MarkRunSql =
        """
        UPDATE saved_queries
        SET last_run_at = @RunAt,
            updated_at = @RunAt
        WHERE id = @Id;
        """;

    private readonly IAppDbConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _schemaSemaphore = new(1, 1);
    private bool _initialized;

    public DapperSavedQueryRepository(IAppDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _schemaSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(CreateSchemaSql, cancellationToken: cancellationToken));
            _initialized = true;
        }
        finally
        {
            _schemaSemaphore.Release();
        }
    }

    public async Task<IReadOnlyList<AppSavedQueryRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<SavedQueryRow>(
            new CommandDefinition(ListSql, cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task<AppSavedQueryRecord?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<SavedQueryRow>(
            new CommandDefinition(GetSql, new { Id = id }, cancellationToken: cancellationToken));

        return row is null ? null : ToRecord(row);
    }

    public async Task SaveAsync(AppSavedQueryRecord query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.QueryText);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            UpsertSql,
            new
            {
                query.Id,
                query.Name,
                query.Description,
                query.QueryText,
                CreatedAt = FormatDateTime(query.CreatedAt),
                UpdatedAt = FormatDateTime(query.UpdatedAt),
                LastRunAt = FormatNullableDateTime(query.LastRunAt)
            },
            cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            DeleteSql,
            new { Id = id },
            cancellationToken: cancellationToken));
    }

    public async Task MarkRunAsync(string id, DateTime runAt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            MarkRunSql,
            new { Id = id, RunAt = FormatDateTime(runAt) },
            cancellationToken: cancellationToken));
    }

    private static AppSavedQueryRecord ToRecord(SavedQueryRow row) => new AppSavedQueryRecord(
            row.Id,
            row.Name,
            row.Description,
            row.QueryText,
            ParseDateTime(row.CreatedAt),
            ParseDateTime(row.UpdatedAt),
            ParseNullableDateTime(row.LastRunAt));

    private static string FormatDateTime(DateTime value) => NormalizeUtc(value).ToString("O");

    private static string? FormatNullableDateTime(DateTime? value) => value is null ? null : FormatDateTime(value.Value);

    private static DateTime ParseDateTime(string value) => DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);

    private static DateTime? ParseNullableDateTime(string? value) => string.IsNullOrWhiteSpace(value) ? null : ParseDateTime(value);

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public void Dispose() => ((IDisposable)_schemaSemaphore).Dispose();

    private sealed class SavedQueryRow
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string QueryText { get; init; } = string.Empty;
        public string CreatedAt { get; init; } = string.Empty;
        public string UpdatedAt { get; init; } = string.Empty;
        public string? LastRunAt { get; init; }
    }
}
