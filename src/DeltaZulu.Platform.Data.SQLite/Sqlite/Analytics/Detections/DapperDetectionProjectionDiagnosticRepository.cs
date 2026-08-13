using Dapper;
using DeltaZulu.Platform.Domain.Analytics.Detections;
using static DeltaZulu.Platform.Data.Sqlite.Analytics.SqliteDateTimeHelpers;

namespace DeltaZulu.Platform.Data.Sqlite.Analytics.Detections;

public sealed class DapperDetectionProjectionDiagnosticRepository : DapperRepositoryBase, IDetectionProjectionDiagnosticRepository
{
    private const string CreateSchemaSql =
        """
        CREATE TABLE IF NOT EXISTS detection_projection_diagnostics (
            id TEXT PRIMARY KEY,
            detection_id TEXT NOT NULL,
            accepted_version_id TEXT NOT NULL,
            reason TEXT NOT NULL,
            message TEXT NOT NULL,
            created_at_utc TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_detection_projection_diagnostics_detection
            ON detection_projection_diagnostics (detection_id, created_at_utc DESC);

        CREATE INDEX IF NOT EXISTS idx_detection_projection_diagnostics_recent
            ON detection_projection_diagnostics (created_at_utc DESC);
        """;

    private const string UpsertSql =
        """
        INSERT INTO detection_projection_diagnostics (
            id, detection_id, accepted_version_id, reason, message, created_at_utc
        )
        VALUES (
            @Id, @DetectionId, @AcceptedVersionId, @Reason, @Message, @CreatedAtUtc
        )
        ON CONFLICT(id) DO UPDATE SET
            reason = excluded.reason,
            message = excluded.message,
            created_at_utc = excluded.created_at_utc;
        """;

    private const string ClearSql =
        """
        DELETE FROM detection_projection_diagnostics
        WHERE id = @Id;
        """;

    private const string ListRecentSql =
        """
        SELECT
            id AS Id,
            detection_id AS DetectionId,
            accepted_version_id AS AcceptedVersionId,
            reason AS Reason,
            message AS Message,
            created_at_utc AS CreatedAtUtc
        FROM detection_projection_diagnostics
        ORDER BY created_at_utc DESC
        LIMIT @Count;
        """;

    private const string ListByDetectionSql =
        """
        SELECT
            id AS Id,
            detection_id AS DetectionId,
            accepted_version_id AS AcceptedVersionId,
            reason AS Reason,
            message AS Message,
            created_at_utc AS CreatedAtUtc
        FROM detection_projection_diagnostics
        WHERE detection_id = @DetectionId
        ORDER BY created_at_utc DESC;
        """;

    public DapperDetectionProjectionDiagnosticRepository(IAppDbConnectionFactory connectionFactory)
        : base(connectionFactory, CreateSchemaSql)
    {
    }

    public async Task SaveAsync(DetectionProjectionDiagnostic diagnostic, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            UpsertSql,
            new {
                diagnostic.Id,
                diagnostic.DetectionId,
                diagnostic.AcceptedVersionId,
                Reason = diagnostic.Reason.ToString(),
                diagnostic.Message,
                CreatedAtUtc = Format(diagnostic.CreatedAtUtc)
            },
            cancellationToken: cancellationToken));
    }

    public async Task ClearAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            ClearSql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<DetectionProjectionDiagnostic>> ListRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<DiagnosticRow>(new CommandDefinition(
            ListRecentSql, new { Count = count }, cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task<IReadOnlyList<DetectionProjectionDiagnostic>> ListByDetectionAsync(string detectionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detectionId);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<DiagnosticRow>(new CommandDefinition(
            ListByDetectionSql, new { DetectionId = detectionId }, cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    private static DetectionProjectionDiagnostic ToRecord(DiagnosticRow row) => new(
        row.Id,
        row.DetectionId,
        row.AcceptedVersionId,
        Enum.Parse<DetectionProjectionDiagnosticReason>(row.Reason),
        row.Message,
        Parse(row.CreatedAtUtc));

    private sealed class DiagnosticRow
    {
        public string Id { get; init; } = string.Empty;
        public string DetectionId { get; init; } = string.Empty;
        public string AcceptedVersionId { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string CreatedAtUtc { get; init; } = string.Empty;
    }
}
