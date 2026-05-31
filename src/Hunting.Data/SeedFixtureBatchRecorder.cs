namespace Hunting.Data;

using DuckDB.NET.Data;

/// <summary>
/// Records governed seed fixture batch metadata into internal.seed_batches.
/// This component records batch application metadata only; it does not execute seed SQL.
/// </summary>
public sealed class SeedFixtureBatchRecorder
{
    public const string SeedBatchesTable = "internal.seed_batches";
    public const string DefaultCatalogVersion = "phase-1c";

    private readonly DuckDbConnectionFactory _connectionFactory;

    public SeedFixtureBatchRecorder(DuckDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public IReadOnlyList<SeedFixtureBatchRecord> RecordAppliedSeedBatches(
        IEnumerable<SeedFixtureBatch> batches,
        string catalogVersion = DefaultCatalogVersion)
    {
        ArgumentNullException.ThrowIfNull(batches);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogVersion);

        var batchList = batches
            .OrderBy(static batch => batch.BatchId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var conn = _connectionFactory.GetConnection();
        using var cmd = conn.CreateCommand();

        foreach (var batch in batchList)
        {
            Upsert(cmd, batch, catalogVersion);
        }

        return ReadRecordedSeedBatches();
    }

    public IReadOnlyList<SeedFixtureBatchRecord> ReadRecordedSeedBatches()
    {
        var rows = new List<SeedFixtureBatchRecord>();
        var conn = _connectionFactory.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT batch_id, table_name, source_name, scenario, row_count, content_hash, catalog_version
            FROM internal.seed_batches
            ORDER BY batch_id
            """;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new SeedFixtureBatchRecord(
                BatchId: reader.GetString(0),
                TableName: reader.GetString(1),
                SourceName: reader.GetString(2),
                Scenario: reader.GetString(3),
                RowCount: reader.GetInt64(4),
                ContentHash: reader.GetString(5),
                CatalogVersion: reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return rows;
    }

    public bool HasMatchingRecordedBatch(SeedFixtureBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var conn = _connectionFactory.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"""
            SELECT count(*)
            FROM {SeedBatchesTable}
            WHERE batch_id = '{EscapeSql(batch.BatchId)}'
              AND table_name = '{EscapeSql(batch.TableName)}'
              AND source_name = '{EscapeSql(batch.SourceName)}'
              AND scenario = '{EscapeSql(batch.Scenario)}'
              AND row_count = {batch.RowCount}
              AND content_hash = '{EscapeSql(batch.ContentHash)}'
            """;

        return Convert.ToInt64(cmd.ExecuteScalar()) == 1;
    }

    private static void Upsert(
        DuckDBCommand cmd,
        SeedFixtureBatch batch,
        string catalogVersion)
    {
        var batchId = EscapeSql(batch.BatchId);
        var tableName = EscapeSql(batch.TableName);
        var sourceName = EscapeSql(batch.SourceName);
        var scenario = EscapeSql(batch.Scenario);
        var contentHash = EscapeSql(batch.ContentHash);
        var version = EscapeSql(batch.CatalogVersion ?? catalogVersion);

        cmd.CommandText =
            $"""
            DELETE FROM {SeedBatchesTable}
            WHERE batch_id = '{batchId}'
            """;
        cmd.ExecuteNonQuery();

        cmd.CommandText =
            $"""
            INSERT INTO {SeedBatchesTable}
                (batch_id, table_name, source_name, scenario, row_count, content_hash, catalog_version, applied_at)
            VALUES
                ('{batchId}', '{tableName}', '{sourceName}', '{scenario}', {batch.RowCount}, '{contentHash}', '{version}', current_timestamp)
            """;
        cmd.ExecuteNonQuery();
    }

    private static string EscapeSql(string value) => value.Replace("'", "''");
}

public sealed record SeedFixtureBatchRecord(
    string BatchId,
    string TableName,
    string SourceName,
    string Scenario,
    long RowCount,
    string ContentHash,
    string? CatalogVersion);