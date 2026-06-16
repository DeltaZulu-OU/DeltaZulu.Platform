
using Dapper;
using DeltaZulu.Platform.Data.Sqlite.Analytics;
using AppISavedQueryRepository = DeltaZulu.Platform.Domain.Analytics.SavedQueries.ISavedQueryRepository;
using AppSavedQueryRecord = DeltaZulu.Platform.Domain.Analytics.SavedQueries.SavedQueryRecord;
using AppSavedQueryPage = DeltaZulu.Platform.Domain.Analytics.PageResult<DeltaZulu.Platform.Domain.Analytics.SavedQueries.SavedQueryRecord>;
using static DeltaZulu.Platform.Data.Sqlite.Analytics.SqliteDateTimeHelpers;

namespace DeltaZulu.Platform.Data.Sqlite.Analytics.SavedQueries;
public sealed class DapperSavedQueryRepository : DapperRepositoryBase, AppISavedQueryRepository
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

    private const string SearchSql =
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
        WHERE @SearchText IS NULL
            OR name LIKE @SearchPattern ESCAPE '\' COLLATE NOCASE
            OR description LIKE @SearchPattern ESCAPE '\' COLLATE NOCASE
        ORDER BY updated_at DESC, name ASC
        LIMIT @Limit OFFSET @Offset;
        """;

    private const string SearchCountSql =
        """
        SELECT COUNT(*)
        FROM saved_queries
        WHERE @SearchText IS NULL
            OR name LIKE @SearchPattern ESCAPE '\' COLLATE NOCASE
            OR description LIKE @SearchPattern ESCAPE '\' COLLATE NOCASE;
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


    public DapperSavedQueryRepository(IAppDbConnectionFactory connectionFactory)
        : base(connectionFactory, CreateSchemaSql)
    {
    }


    public async Task<IReadOnlyList<AppSavedQueryRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<SavedQueryRow>(
            new CommandDefinition(ListSql, cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task<AppSavedQueryPage> SearchAsync(
        string? searchText,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        await EnsureInitializedAsync(cancellationToken);

        var boundedLimit = Math.Min(limit, 100);
        var normalizedSearch = NormalizeLikeSearch(searchText);
        var parameters = new
        {
            SearchText = normalizedSearch,
            SearchPattern = normalizedSearch is null ? null : $"%{EscapeLikePattern(normalizedSearch)}%",
            Offset = offset,
            Limit = boundedLimit
        };

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(SearchCountSql, parameters, cancellationToken: cancellationToken));
        var rows = await connection.QueryAsync<SavedQueryRow>(
            new CommandDefinition(SearchSql, parameters, cancellationToken: cancellationToken));

        return new AppSavedQueryPage(
            rows.Select(ToRecord).ToArray(),
            totalCount,
            offset,
            boundedLimit);
    }

    public async Task<AppSavedQueryRecord?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

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

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            UpsertSql,
            new {
                query.Id,
                query.Name,
                query.Description,
                query.QueryText,
                CreatedAt = Format(query.CreatedAt),
                UpdatedAt = Format(query.UpdatedAt),
                LastRunAt = FormatNullable(query.LastRunAt)
            },
            cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            DeleteSql,
            new { Id = id },
            cancellationToken: cancellationToken));
    }

    public async Task MarkRunAsync(string id, DateTime runAt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            MarkRunSql,
            new { Id = id, RunAt = Format(runAt) },
            cancellationToken: cancellationToken));
    }

    private static AppSavedQueryRecord ToRecord(SavedQueryRow row) => new AppSavedQueryRecord(
            row.Id,
            row.Name,
            row.Description,
            row.QueryText,
            Parse(row.CreatedAt),
            Parse(row.UpdatedAt),
            ParseNullable(row.LastRunAt));


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
