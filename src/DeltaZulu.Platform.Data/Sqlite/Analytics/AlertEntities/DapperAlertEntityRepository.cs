
using Dapper;
using DeltaZulu.Platform.Domain.Analytics.AlertEntities;

namespace DeltaZulu.Platform.Data.Sqlite.Analytics.AlertEntities;
public sealed class DapperAlertEntityRepository : IAlertEntityRepository, IDisposable
{
    private const string CreateSchemaSql =
        """
        CREATE TABLE IF NOT EXISTS alert_entities (
            id TEXT PRIMARY KEY,
            alert_id TEXT NOT NULL,
            entity_type TEXT NOT NULL,
            entity_value TEXT NOT NULL,
            role TEXT NOT NULL,
            specificity_weight REAL NOT NULL DEFAULT 1.0,
            criticality_weight REAL NOT NULL DEFAULT 1.0,
            is_high_fanout INTEGER NOT NULL DEFAULT 0,
            created_at_utc TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_alert_entities_alert_id
            ON alert_entities (alert_id);

        CREATE INDEX IF NOT EXISTS idx_alert_entities_entity_lookup
            ON alert_entities (entity_type, entity_value, alert_id);

        CREATE INDEX IF NOT EXISTS idx_alert_entities_high_fanout
            ON alert_entities (is_high_fanout, entity_type);
        """;

    private const string ListByAlertSql =
        """
        SELECT
            id AS Id,
            alert_id AS AlertId,
            entity_type AS EntityType,
            entity_value AS EntityValue,
            role AS Role,
            specificity_weight AS SpecificityWeight,
            criticality_weight AS CriticalityWeight,
            is_high_fanout AS IsHighFanout,
            created_at_utc AS CreatedAtUtc
        FROM alert_entities
        WHERE alert_id = @AlertId
        ORDER BY specificity_weight DESC, criticality_weight DESC;
        """;

    private const string ListByEntityValueSql =
        """
        SELECT
            id AS Id,
            alert_id AS AlertId,
            entity_type AS EntityType,
            entity_value AS EntityValue,
            role AS Role,
            specificity_weight AS SpecificityWeight,
            criticality_weight AS CriticalityWeight,
            is_high_fanout AS IsHighFanout,
            created_at_utc AS CreatedAtUtc
        FROM alert_entities
        WHERE entity_type = @EntityType
          AND entity_value = @EntityValue
        ORDER BY created_at_utc DESC;
        """;

    private const string InsertSql =
        """
        INSERT OR IGNORE INTO alert_entities (
            id, alert_id, entity_type, entity_value, role,
            specificity_weight, criticality_weight, is_high_fanout,
            created_at_utc
        )
        VALUES (
            @Id, @AlertId, @EntityType, @EntityValue, @Role,
            @SpecificityWeight, @CriticalityWeight, @IsHighFanout,
            @CreatedAtUtc
        );
        """;

    private readonly IAppDbConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _schemaSemaphore = new(1, 1);
    private bool _initialized;

    public DapperAlertEntityRepository(IAppDbConnectionFactory connectionFactory)
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

    public async Task<IReadOnlyList<AlertEntityRecord>> ListByAlertAsync(string alertId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alertId);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<AlertEntityRow>(
            new CommandDefinition(ListByAlertSql, new { AlertId = alertId }, cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task<IReadOnlyList<AlertEntityRecord>> ListByEntityValueAsync(string entityType, string entityValue, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityValue);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<AlertEntityRow>(
            new CommandDefinition(ListByEntityValueSql, new { EntityType = entityType, EntityValue = entityValue }, cancellationToken: cancellationToken));

        return rows.Select(ToRecord).ToArray();
    }

    public async Task SaveBatchAsync(IReadOnlyList<AlertEntityRecord> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        if (entities.Count == 0)
        {
            return;
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var entity in entities)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entity.Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(entity.AlertId);
            ArgumentException.ThrowIfNullOrWhiteSpace(entity.EntityType);
            ArgumentException.ThrowIfNullOrWhiteSpace(entity.EntityValue);

            await connection.ExecuteAsync(new CommandDefinition(
                InsertSql,
                new {
                    entity.Id,
                    entity.AlertId,
                    entity.EntityType,
                    entity.EntityValue,
                    entity.Role,
                    entity.SpecificityWeight,
                    entity.CriticalityWeight,
                    IsHighFanout = entity.IsHighFanout ? 1 : 0,
                    CreatedAtUtc = FormatDateTime(entity.CreatedAtUtc)
                },
                cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static AlertEntityRecord ToRecord(AlertEntityRow row) => new AlertEntityRecord(
            row.Id,
            row.AlertId,
            row.EntityType,
            row.EntityValue,
            row.Role,
            row.SpecificityWeight,
            row.CriticalityWeight,
            row.IsHighFanout != 0,
            ParseDateTime(row.CreatedAtUtc));

    private static string FormatDateTime(DateTime value) => NormalizeUtc(value).ToString("O");

    private static DateTime ParseDateTime(string value) => DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public void Dispose() => ((IDisposable)_schemaSemaphore).Dispose();

    private sealed class AlertEntityRow
    {
        public string Id { get; init; } = string.Empty;
        public string AlertId { get; init; } = string.Empty;
        public string EntityType { get; init; } = string.Empty;
        public string EntityValue { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public double SpecificityWeight { get; init; }
        public double CriticalityWeight { get; init; }
        public int IsHighFanout { get; init; }
        public string CreatedAtUtc { get; init; } = string.Empty;
    }
}
