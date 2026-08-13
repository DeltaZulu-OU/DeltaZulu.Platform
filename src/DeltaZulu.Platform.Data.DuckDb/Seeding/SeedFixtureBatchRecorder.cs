using Dapper;
using DeltaZulu.Platform.Data.DuckDb;

namespace DeltaZulu.Platform.Data.Seeding;

/// <summary>
/// Records governed seed fixture batch metadata into internal.SeedBatches.
/// This component records batch application metadata only; it does not execute seed SQL.
/// </summary>
public sealed class SeedFixtureBatchRecorder
{
    public const string SeedBatchesTable = "internal.SeedBatches";
    public const string DefaultCatalogVersion = "phase-1c";

    private const string DeleteSql =
        $"""
        DELETE FROM {SeedBatchesTable}
        WHERE BatchId = $BatchId
        """;

    private const string InsertSql =
        $"""
        INSERT INTO {SeedBatchesTable}
            (BatchId, TableName, SourceName, Scenario, RowCount, ContentHash, CatalogVersion, AppliedAt)
        VALUES
            ($BatchId, $TableName, $SourceName, $Scenario, $RowCount, $ContentHash, $CatalogVersion, current_timestamp)
        """;

    private const string SelectSql =
        """
        SELECT
            BatchId AS BatchId,
            TableName AS TableName,
            SourceName AS SourceName,
            Scenario AS Scenario,
            RowCount AS RowCount,
            ContentHash AS ContentHash,
            CatalogVersion AS CatalogVersion
        FROM internal.SeedBatches
        ORDER BY BatchId
        """;

    private const string CountMatchingSql =
        $"""
        SELECT count(*)
        FROM {SeedBatchesTable}
        WHERE BatchId = $BatchId
          AND TableName = $TableName
          AND SourceName = $SourceName
          AND Scenario = $Scenario
          AND RowCount = $RowCount
          AND ContentHash = $ContentHash
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
        string catalogVersion) => new {
            batch.BatchId,
            batch.TableName,
            batch.SourceName,
            batch.Scenario,
            batch.RowCount,
            batch.ContentHash,
            CatalogVersion = batch.CatalogVersion ?? catalogVersion
        };
}

public sealed record SeedFixtureBatchRecord(
    string BatchId,
    string TableName,
    string SourceName,
    string Scenario,
    long RowCount,
    string ContentHash,
    string? CatalogVersion);