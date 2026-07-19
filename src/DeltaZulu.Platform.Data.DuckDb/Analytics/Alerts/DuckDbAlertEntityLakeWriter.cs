using Dapper;
using DeltaZulu.Platform.Domain.Analytics.AlertEntities;

namespace DeltaZulu.Platform.Data.DuckDb.Analytics.Alerts;

/// <summary>DuckDB-backed append-only writer for entities extracted from alert evidence.</summary>
public sealed class DuckDbAlertEntityLakeWriter(DuckDbConnectionFactory connectionFactory) : IAlertEntityLakeWriter
{
    private const string CreateSchemaSql = """
        CREATE SCHEMA IF NOT EXISTS lake;
        CREATE TABLE IF NOT EXISTS lake.alert_entities (
            id VARCHAR NOT NULL, alert_id VARCHAR NOT NULL, entity_type VARCHAR NOT NULL,
            entity_value VARCHAR NOT NULL, role VARCHAR NOT NULL, specificity_weight DOUBLE NOT NULL,
            criticality_weight DOUBLE NOT NULL, is_high_fanout BOOLEAN NOT NULL, entity_value_json JSON,
            entity_type_contract VARCHAR NOT NULL, created_at_utc TIMESTAMP NOT NULL
        );
        """;
    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        connectionFactory.GetConnection().Execute(CreateSchemaSql);
        return Task.CompletedTask;
    }

    public async Task AppendBatchAsync(IReadOnlyList<AlertEntityRecord> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        if (entities.Count == 0) return;
        await EnsureInitializedAsync(cancellationToken);
        var connection = connectionFactory.GetConnection();
        foreach (var entity in entities)
        {
            ArgumentNullException.ThrowIfNull(entity);
            cancellationToken.ThrowIfCancellationRequested();
            connection.Execute(BuildInsertSql(entity));
        }
    }

    private static string BuildInsertSql(AlertEntityRecord entity) => $"""
        INSERT INTO lake.alert_entities (
            id, alert_id, entity_type, entity_value, role, specificity_weight, criticality_weight,
            is_high_fanout, entity_value_json, entity_type_contract, created_at_utc)
        VALUES (
            {StringLiteral(entity.Id)}, {StringLiteral(entity.AlertId)}, {StringLiteral(entity.EntityType)},
            {StringLiteral(entity.EntityValue)}, {StringLiteral(entity.Role)},
            {entity.SpecificityWeight.ToString(System.Globalization.CultureInfo.InvariantCulture)},
            {entity.CriticalityWeight.ToString(System.Globalization.CultureInfo.InvariantCulture)},
            {entity.IsHighFanout.ToString().ToLowerInvariant()}, {NullableJsonLiteral(entity.EntityValueJson)},
            {StringLiteral(entity.EntityTypeContract)}, {TimestampLiteral(entity.CreatedAtUtc)});
        """;

    private static string NullableJsonLiteral(string? value) => value is null ? "NULL" : $"CAST({StringLiteral(value)} AS JSON)";

    private static string StringLiteral(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private static string TimestampLiteral(DateTime value) => StringLiteral(value.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
}
