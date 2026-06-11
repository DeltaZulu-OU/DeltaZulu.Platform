
using Dapper;
using AppIVisualizationRepository = DeltaZulu.Platform.Domain.Analytics.Visualizations.IVisualizationRepository;
using AppVisualizationRecord = DeltaZulu.Platform.Domain.Analytics.Visualizations.VisualizationRecord;

namespace DeltaZulu.Platform.Data.Sqlite.Analytics.Visualizations;
public sealed class DapperVisualizationRepository : AppIVisualizationRepository, IDisposable
{
    private const string CreateSchemaSql =
        """
        CREATE TABLE IF NOT EXISTS visualizations (
            id TEXT PRIMARY KEY,
            query_id TEXT NOT NULL,
            name TEXT NOT NULL,
            description TEXT NULL,
            kind TEXT NOT NULL,
            spec_json TEXT NOT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_visualizations_query_id
            ON visualizations (query_id);

        CREATE INDEX IF NOT EXISTS idx_visualizations_updated_at
            ON visualizations (updated_at DESC, name ASC);
        """;

    private const string ListSql =
        """
        SELECT
            id AS Id,
            query_id AS QueryId,
            name AS Name,
            description AS Description,
            kind AS Kind,
            spec_json AS SpecJson,
            created_at AS CreatedAt,
            updated_at AS UpdatedAt
        FROM visualizations
        ORDER BY updated_at DESC, name ASC;
        """;

    private const string ListByQuerySql =
        """
        SELECT
            id AS Id,
            query_id AS QueryId,
            name AS Name,
            description AS Description,
            kind AS Kind,
            spec_json AS SpecJson,
            created_at AS CreatedAt,
            updated_at AS UpdatedAt
        FROM visualizations
        WHERE query_id = @QueryId
        ORDER BY updated_at DESC, name ASC;
        """;

    private const string GetSql =
        """
        SELECT
            id AS Id,
            query_id AS QueryId,
            name AS Name,
            description AS Description,
            kind AS Kind,
            spec_json AS SpecJson,
            created_at AS CreatedAt,
            updated_at AS UpdatedAt
        FROM visualizations
        WHERE id = @Id;
        """;

    private const string UpsertSql =
        """
        INSERT INTO visualizations (
            id,
            query_id,
            name,
            description,
            kind,
            spec_json,
            created_at,
            updated_at
        )
        VALUES (
            @Id,
            @QueryId,
            @Name,
            @Description,
            @Kind,
            @SpecJson,
            @CreatedAt,
            @UpdatedAt
        )
        ON CONFLICT(id) DO UPDATE SET
            query_id = excluded.query_id,
            name = excluded.name,
            description = excluded.description,
            kind = excluded.kind,
            spec_json = excluded.spec_json,
            updated_at = excluded.updated_at;
        """;

    private const string DeleteSql =
        """
        DELETE FROM visualizations
        WHERE id = @Id;
        """;

    private readonly IAppDbConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _schemaSemaphore = new(1, 1);
    private bool _initialized;

    public DapperVisualizationRepository(IAppDbConnectionFactory connectionFactory)
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

    public async Task<IReadOnlyList<AppVisualizationRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<VisualizationRow>(
            new CommandDefinition(ListSql, cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task<IReadOnlyList<AppVisualizationRecord>> ListByQueryAsync(
        string queryId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryId);
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<VisualizationRow>(
            new CommandDefinition(ListByQuerySql, new { QueryId = queryId }, cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task<AppVisualizationRecord?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<VisualizationRow>(
            new CommandDefinition(GetSql, new { Id = id }, cancellationToken: cancellationToken));

        return row is null ? null : ToRecord(row);
    }

    public async Task SaveAsync(AppVisualizationRecord visualization, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(visualization);
        ArgumentException.ThrowIfNullOrWhiteSpace(visualization.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(visualization.QueryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(visualization.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(visualization.Kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(visualization.SpecJson);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            UpsertSql,
            new {
                visualization.Id,
                visualization.QueryId,
                visualization.Name,
                visualization.Description,
                visualization.Kind,
                visualization.SpecJson,
                CreatedAt = FormatDateTime(visualization.CreatedAt),
                UpdatedAt = FormatDateTime(visualization.UpdatedAt)
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

    private static AppVisualizationRecord ToRecord(VisualizationRow row) => new AppVisualizationRecord(
            row.Id,
            row.QueryId,
            row.Name,
            row.Description,
            row.Kind,
            row.SpecJson,
            ParseDateTime(row.CreatedAt),
            ParseDateTime(row.UpdatedAt));

    private static string FormatDateTime(DateTime value) => NormalizeUtc(value).ToString("O");

    private static DateTime ParseDateTime(string value) => DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public void Dispose() => ((IDisposable)_schemaSemaphore).Dispose();

    private sealed class VisualizationRow
    {
        public string Id { get; init; } = string.Empty;
        public string QueryId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string Kind { get; init; } = string.Empty;
        public string SpecJson { get; init; } = string.Empty;
        public string CreatedAt { get; init; } = string.Empty;
        public string UpdatedAt { get; init; } = string.Empty;
    }
}