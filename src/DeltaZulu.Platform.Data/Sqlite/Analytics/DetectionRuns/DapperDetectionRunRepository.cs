
using Dapper;
using DeltaZulu.Platform.Domain.Analytics.DetectionRuns;
using static DeltaZulu.Platform.Data.Sqlite.Analytics.SqliteDateTimeHelpers;

namespace DeltaZulu.Platform.Data.Sqlite.Analytics.DetectionRuns;
public sealed class DapperDetectionRunRepository : IDetectionRunRepository, IDisposable
{
    private const string CreateSchemaSql =
        """
        CREATE TABLE IF NOT EXISTS detection_runs (
            id TEXT PRIMARY KEY,
            detection_id TEXT NOT NULL,
            detection_version INTEGER NOT NULL,
            rule_hash TEXT NOT NULL,
            execution_window_start_utc TEXT NOT NULL,
            execution_window_end_utc TEXT NOT NULL,
            status TEXT NOT NULL,
            result_count INTEGER NOT NULL DEFAULT 0,
            duration_ms INTEGER NOT NULL DEFAULT 0,
            error_message TEXT NULL,
            query_hash TEXT NOT NULL,
            started_at_utc TEXT NOT NULL,
            completed_at_utc TEXT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_detection_runs_detection_id
            ON detection_runs (detection_id, started_at_utc DESC);

        CREATE INDEX IF NOT EXISTS idx_detection_runs_status
            ON detection_runs (status, started_at_utc DESC);
        """;

    private const string ListByDetectionSql =
        """
        SELECT
            id AS Id,
            detection_id AS DetectionId,
            detection_version AS DetectionVersion,
            rule_hash AS RuleHash,
            execution_window_start_utc AS ExecutionWindowStartUtc,
            execution_window_end_utc AS ExecutionWindowEndUtc,
            status AS Status,
            result_count AS ResultCount,
            duration_ms AS DurationMs,
            error_message AS ErrorMessage,
            query_hash AS QueryHash,
            started_at_utc AS StartedAtUtc,
            completed_at_utc AS CompletedAtUtc
        FROM detection_runs
        WHERE detection_id = @DetectionId
        ORDER BY started_at_utc DESC;
        """;

    private const string GetSql =
        """
        SELECT
            id AS Id,
            detection_id AS DetectionId,
            detection_version AS DetectionVersion,
            rule_hash AS RuleHash,
            execution_window_start_utc AS ExecutionWindowStartUtc,
            execution_window_end_utc AS ExecutionWindowEndUtc,
            status AS Status,
            result_count AS ResultCount,
            duration_ms AS DurationMs,
            error_message AS ErrorMessage,
            query_hash AS QueryHash,
            started_at_utc AS StartedAtUtc,
            completed_at_utc AS CompletedAtUtc
        FROM detection_runs
        WHERE id = @Id;
        """;

    private const string UpsertSql =
        """
        INSERT INTO detection_runs (
            id, detection_id, detection_version, rule_hash,
            execution_window_start_utc, execution_window_end_utc,
            status, result_count, duration_ms, error_message,
            query_hash, started_at_utc, completed_at_utc
        )
        VALUES (
            @Id, @DetectionId, @DetectionVersion, @RuleHash,
            @ExecutionWindowStartUtc, @ExecutionWindowEndUtc,
            @Status, @ResultCount, @DurationMs, @ErrorMessage,
            @QueryHash, @StartedAtUtc, @CompletedAtUtc
        )
        ON CONFLICT(id) DO UPDATE SET
            status = excluded.status,
            result_count = excluded.result_count,
            duration_ms = excluded.duration_ms,
            error_message = excluded.error_message,
            completed_at_utc = excluded.completed_at_utc;
        """;

    private readonly IAppDbConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _schemaSemaphore = new(1, 1);
    private bool _initialized;

    public DapperDetectionRunRepository(IAppDbConnectionFactory connectionFactory)
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

            await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(CreateSchemaSql, cancellationToken: cancellationToken));
            _initialized = true;
        }
        finally
        {
            _schemaSemaphore.Release();
        }
    }

    public async Task<IReadOnlyList<DetectionRunRecord>> ListByDetectionAsync(string detectionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detectionId);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<DetectionRunRow>(
            new CommandDefinition(ListByDetectionSql, new { DetectionId = detectionId }, cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task<DetectionRunRecord?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<DetectionRunRow>(
            new CommandDefinition(GetSql, new { Id = id }, cancellationToken: cancellationToken));

        return row is null ? null : ToRecord(row);
    }

    public async Task SaveAsync(DetectionRunRecord run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(run.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(run.DetectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(run.RuleHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(run.QueryHash);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            UpsertSql,
            new {
                run.Id,
                run.DetectionId,
                run.DetectionVersion,
                run.RuleHash,
                ExecutionWindowStartUtc = Format(run.ExecutionWindowStartUtc),
                ExecutionWindowEndUtc = Format(run.ExecutionWindowEndUtc),
                run.Status,
                run.ResultCount,
                run.DurationMs,
                run.ErrorMessage,
                run.QueryHash,
                StartedAtUtc = Format(run.StartedAtUtc),
                CompletedAtUtc = FormatNullable(run.CompletedAtUtc)
            },
            cancellationToken: cancellationToken));
    }

    private static DetectionRunRecord ToRecord(DetectionRunRow row) => new DetectionRunRecord(
            row.Id,
            row.DetectionId,
            row.DetectionVersion,
            row.RuleHash,
            Parse(row.ExecutionWindowStartUtc),
            Parse(row.ExecutionWindowEndUtc),
            row.Status,
            row.ResultCount,
            row.DurationMs,
            row.ErrorMessage,
            row.QueryHash,
            Parse(row.StartedAtUtc),
            ParseNullable(row.CompletedAtUtc));

    public void Dispose() => ((IDisposable)_schemaSemaphore).Dispose();

    private sealed class DetectionRunRow
    {
        public string Id { get; init; } = string.Empty;
        public string DetectionId { get; init; } = string.Empty;
        public int DetectionVersion { get; init; }
        public string RuleHash { get; init; } = string.Empty;
        public string ExecutionWindowStartUtc { get; init; } = string.Empty;
        public string ExecutionWindowEndUtc { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public int ResultCount { get; init; }
        public long DurationMs { get; init; }
        public string? ErrorMessage { get; init; }
        public string QueryHash { get; init; } = string.Empty;
        public string StartedAtUtc { get; init; } = string.Empty;
        public string? CompletedAtUtc { get; init; }
    }
}