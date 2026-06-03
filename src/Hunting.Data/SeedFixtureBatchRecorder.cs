namespace Hunting.Data;

using Dapper;

/// <summary>
/// Records governed seed fixture batch metadata into internal.seed_batches.
/// This component records batch application metadata only; it does not execute seed SQL.
/// </summary>
public sealed class SeedFixtureBatchRecorder
{
    public const string SeedBatchesTable = "internal.seed_batches";
    public const string DefaultCatalogVersion = "phase-1c";

    private const string DeleteSql =
        $"""
        DELETE FROM {SeedBatchesTable}
        WHERE batch_id = @BatchId
        """;

    private const string InsertSql =
        $"""
        INSERT INTO {SeedBatchesTable}
            (batch_id, table_name, source_name, scenario, row_count, content_hash, catalog_version, applied_at)
        VALUES
            (@BatchId, @TableName, @SourceName, @Scenario, @RowCount, @ContentHash, @CatalogVersion, current_timestamp)
        """;

    private const string SelectSql =
        """
        SELECT
            batch_id AS BatchId,
            table_name AS TableName,
            source_name AS SourceName,
            scenario AS Scenario,
            row_count AS RowCount,
            content_hash AS ContentHash,
            catalog_version AS CatalogVersion
        FROM internal.seed_batches
        ORDER BY batch_id
        """;

    private const string CountMatchingSql =
        $"""
        SELECT count(*)
        FROM {SeedBatchesTable}
        WHERE batch_id = @BatchId
          AND table_name = @TableName
          AND source_name = @SourceName
          AND scenario = @Scenario
          AND row_count = @RowCount
          AND content_hash = @ContentHash
        """;

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
        using var tx = conn.BeginTransaction();

        foreach (var batch in batchList)
        {
            var parameters = ToParameters(batch, catalogVersion);
            conn.Execute(DeleteSql, parameters, tx);
            conn.Execute(InsertSql, parameters, tx);
        }

        tx.Commit();
        return ReadRecordedSeedBatches();
    }

    public IReadOnlyList<SeedFixtureBatchRecord> ReadRecordedSeedBatches()
    {
        var conn = _connectionFactory.GetConnection();
        return conn.Query<SeedFixtureBatchRecord>(SelectSql).AsList();
    }

    public bool HasMatchingRecordedBatch(SeedFixtureBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var conn = _connectionFactory.GetConnection();
        return conn.ExecuteScalar<long>(
            CountMatchingSql,
            ToParameters(batch, batch.CatalogVersion ?? DefaultCatalogVersion)) == 1;
    }

    private static object ToParameters(
        SeedFixtureBatch batch,
        string catalogVersion)
    {
        return new
        {
            batch.BatchId,
            batch.TableName,
            batch.SourceName,
            batch.Scenario,
            batch.RowCount,
            batch.ContentHash,
            CatalogVersion = batch.CatalogVersion ?? catalogVersion
        };
    }
}

public sealed record SeedFixtureBatchRecord(
    string BatchId,
    string TableName,
    string SourceName,
    string Scenario,
    long RowCount,
    string ContentHash,
    string? CatalogVersion);
