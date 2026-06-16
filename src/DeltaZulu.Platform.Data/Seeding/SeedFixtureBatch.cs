using System.Security.Cryptography;
using System.Text;

namespace DeltaZulu.Platform.Data.Seeding;

/// <summary>
/// Stable seed fixture hashing utility.
/// </summary>
public static class SeedFixtureBatchHasher
{
    public static string Hash(SeedFixtureBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var payload = string.Join("\n",
        [
            $"batch_id={batch.BatchId}",
            $"table_name={batch.TableName}",
            $"source_name={batch.SourceName}",
            $"scenario={batch.Scenario}",
            $"row_count={batch.RowCount}",
            $"sql={NormalizeSql(batch.Sql)}"
        ]);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string NormalizeSql(string sql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var builder = new StringBuilder(sql.Length);
        var previousWasWhitespace = false;

        foreach (var ch in sql.Replace("\r\n", "\n").Replace('\r', '\n'))
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }

                continue;
            }

            builder.Append(ch);
            previousWasWhitespace = false;
        }

        return builder.ToString().Trim();
    }
}

/// <summary>
/// A governed seed fixture batch targeting one Bronze/internal table.
/// Phase 1C uses this as the unit of seed idempotency, provenance, and repair.
/// </summary>
public sealed class SeedFixtureBatch
{
    public SeedFixtureBatch(
        string BatchId,
        string TableName,
        string SourceName,
        string Scenario,
        string Sql,
        long RowCount,
        string? CatalogVersion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(BatchId);
        ArgumentException.ThrowIfNullOrWhiteSpace(TableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(SourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(Scenario);
        ArgumentException.ThrowIfNullOrWhiteSpace(Sql);

        if (RowCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RowCount), RowCount, "Seed fixture row count cannot be negative.");
        }

        BatchId = BatchId.Trim();
        TableName = TableName.Trim();
        SourceName = SourceName.Trim();
        Scenario = Scenario.Trim();
        Sql = Sql.Trim();
        CatalogVersion = string.IsNullOrWhiteSpace(CatalogVersion) ? null : CatalogVersion.Trim();

        this.BatchId = BatchId;
        this.TableName = TableName;
        this.SourceName = SourceName;
        this.Scenario = Scenario;
        this.Sql = Sql;
        this.RowCount = RowCount;
        this.CatalogVersion = CatalogVersion;
    }

    public string BatchId { get; }
    public string? CatalogVersion { get; }
    public string ContentHash => SeedFixtureBatchHasher.Hash(this);
    public long RowCount { get; }
    public string Scenario { get; }
    public string SourceName { get; }
    public string Sql { get; }
    public string TableName { get; }
}

/// <summary>
/// Factory helpers for creating seed fixture batches from existing per-table seed SQL.
/// </summary>
public static class SeedFixtureBatchFactory
{
    public static IReadOnlyList<SeedFixtureBatch> FromTableSeedSql(
        IReadOnlyDictionary<string, string> seedSqlByTable,
        IReadOnlyDictionary<string, long> expectedRowsByTable,
        IReadOnlyDictionary<string, string> sourceNameByTable,
        string scenario,
        string? catalogVersion = null)
    {
        ArgumentNullException.ThrowIfNull(seedSqlByTable);
        ArgumentNullException.ThrowIfNull(expectedRowsByTable);
        ArgumentNullException.ThrowIfNull(sourceNameByTable);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);

        var batches = new List<SeedFixtureBatch>();

        foreach (var (tableName, sql) in seedSqlByTable.OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!expectedRowsByTable.TryGetValue(tableName, out var rowCount))
            {
                throw new InvalidOperationException($"Missing expected row count for seed table {tableName}.");
            }

            if (!sourceNameByTable.TryGetValue(tableName, out var sourceName))
            {
                throw new InvalidOperationException($"Missing source name for seed table {tableName}.");
            }

            batches.Add(new SeedFixtureBatch(
                BatchId: BuildBatchId(tableName, scenario),
                TableName: tableName,
                SourceName: sourceName,
                Scenario: scenario,
                Sql: sql,
                RowCount: rowCount,
                CatalogVersion: catalogVersion));
        }

        return batches;
    }

    private static string BuildBatchId(string tableName, string scenario)
    {
        var safeTable = tableName
            .Replace('.', '_')
            .Replace('-', '_')
            .ToLowerInvariant();

        var safeScenario = scenario
            .Replace('.', '_')
            .Replace('-', '_')
            .ToLowerInvariant();

        return $"{safeTable}_{safeScenario}";
    }
}