namespace Hunting.Data.QueryHistory;

using Dapper;
using Hunting.Data.Persistence;
using AppIQueryHistoryRepository = Hunting.Application.QueryHistory.IQueryHistoryRepository;
using AppQueryHistoryRecord = Hunting.Application.QueryHistory.QueryHistoryRecord;

public sealed class DapperQueryHistoryRepository : AppIQueryHistoryRepository, IDisposable
{
    private const string CreateSchemaSql =
        """
        CREATE TABLE IF NOT EXISTS query_history (
            id TEXT PRIMARY KEY,
            query_text TEXT NOT NULL,
            executed_at TEXT NOT NULL,
            succeeded INTEGER NOT NULL,
            row_count INTEGER NULL,
            duration_ms INTEGER NULL,
            diagnostic_summary TEXT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_query_history_executed_at
            ON query_history (executed_at DESC);
        """;

    private const string InsertSql =
        """
        INSERT INTO query_history (
            id,
            query_text,
            executed_at,
            succeeded,
            row_count,
            duration_ms,
            diagnostic_summary
        )
        VALUES (
            @Id,
            @QueryText,
            @ExecutedAt,
            @Succeeded,
            @RowCount,
            @DurationMs,
            @DiagnosticSummary
        );
        """;

    private const string ListRecentSql =
        """
        SELECT
            id AS Id,
            query_text AS QueryText,
            executed_at AS ExecutedAt,
            succeeded AS Succeeded,
            row_count AS RowCount,
            duration_ms AS DurationMs,
            diagnostic_summary AS DiagnosticSummary
        FROM query_history
        ORDER BY executed_at DESC
        LIMIT @Limit;
        """;

    private const string ClearSql =
        """
        DELETE FROM query_history;
        """;

    private readonly IAppDbConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _schemaSemaphore = new(1, 1);
    private bool _initialized;

    public DapperQueryHistoryRepository(IAppDbConnectionFactory connectionFactory)
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

    public async Task<IReadOnlyList<AppQueryHistoryRecord>> ListRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be greater than zero.");
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<QueryHistoryRow>(
            new CommandDefinition(ListRecentSql, new { Limit = limit }, cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task AddAsync(AppQueryHistoryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(record.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(record.QueryText);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            InsertSql,
            new
            {
                record.Id,
                record.QueryText,
                ExecutedAt = FormatDateTime(record.ExecutedAt),
                Succeeded = record.Succeeded ? 1 : 0,
                record.RowCount,
                record.DurationMs,
                record.DiagnosticSummary
            },
            cancellationToken: cancellationToken));
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(ClearSql, cancellationToken: cancellationToken));
    }

    private static AppQueryHistoryRecord ToRecord(QueryHistoryRow row) => new AppQueryHistoryRecord(
            row.Id,
            row.QueryText,
            ParseDateTime(row.ExecutedAt),
            row.Succeeded != 0,
            ConvertNullableInt32(row.RowCount),
            row.DurationMs,
            row.DiagnosticSummary);

    private static int? ConvertNullableInt32(long? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value < int.MinValue || value > int.MaxValue)
        {
            throw new InvalidOperationException($"Stored row count is outside Int32 range: {value.Value}.");
        }

        return checked((int)value.Value);
    }

    private static string FormatDateTime(DateTime value) => NormalizeUtc(value).ToString("O");

    private static DateTime ParseDateTime(string value) => DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private sealed class QueryHistoryRow
    {
        public string Id { get; init; } = string.Empty;
        public string QueryText { get; init; } = string.Empty;
        public string ExecutedAt { get; init; } = string.Empty;
        public long Succeeded { get; init; }
        public long? RowCount { get; init; }
        public long? DurationMs { get; init; }
        public string? DiagnosticSummary { get; init; }
    }

    public void Dispose() => _schemaSemaphore.Dispose();
}
