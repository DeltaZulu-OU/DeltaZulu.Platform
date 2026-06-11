using DeltaZulu.Platform.Data.DuckDb;

namespace DeltaZulu.Platform.Data.Seeding;

public enum SeedFixtureBatchApplyPolicy
{
    BlockMismatchedRecordedBatch,
    AllowMismatchedRecordedBatch
}

public enum SeedFixtureBatchApplyStatus
{
    Applied,
    Skipped,
    Blocked
}

/// <summary>
/// Applies governed seed fixture batches with metadata-based idempotency.
/// Matching recorded batches are skipped. Missing batches are executed and recorded.
/// Mismatched recorded batches are reported and blocked by default.
/// </summary>
public sealed class SeedFixtureBatchApplier
{
    private readonly SeedFixtureBatchRecorder _recorder;
    private readonly SchemaApplier _schemaApplier;

    public SeedFixtureBatchApplier(
        SchemaApplier schemaApplier,
        SeedFixtureBatchRecorder recorder)
    {
        _schemaApplier = schemaApplier;
        _recorder = recorder;
    }

    public SeedFixtureBatchApplyReport Apply(
        IEnumerable<SeedFixtureBatch> batches,
        SeedFixtureBatchApplyPolicy policy = SeedFixtureBatchApplyPolicy.BlockMismatchedRecordedBatch,
        string catalogVersion = SeedFixtureBatchRecorder.DefaultCatalogVersion)
    {
        ArgumentNullException.ThrowIfNull(batches);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogVersion);

        var batchList = batches
            .OrderBy(static batch => batch.BatchId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var recorded = _recorder.ReadRecordedSeedBatches()
            .ToDictionary(static row => row.BatchId, StringComparer.OrdinalIgnoreCase);

        var results = new List<SeedFixtureBatchApplyResult>();

        foreach (var batch in batchList)
        {
            if (!recorded.TryGetValue(batch.BatchId, out var existing))
            {
                _schemaApplier.ExecuteRaw(batch.Sql);
                _recorder.RecordAppliedSeedBatches([batch], catalogVersion);
                results.Add(SeedFixtureBatchApplyResult.Applied(batch));
                continue;
            }

            if (Matches(existing, batch))
            {
                results.Add(SeedFixtureBatchApplyResult.Skipped(batch, "Matching seed batch metadata is already recorded."));
                continue;
            }

            var result = SeedFixtureBatchApplyResult.Blocked(
                batch,
                $"Recorded seed batch {batch.BatchId} does not match the current fixture batch metadata.");

            results.Add(result);

            if (policy == SeedFixtureBatchApplyPolicy.BlockMismatchedRecordedBatch)
            {
                throw new SeedFixtureBatchMismatchException(
                    new SeedFixtureBatchApplyReport(results, HadBlockedBatch: true));
            }

            _schemaApplier.ExecuteRaw(batch.Sql);
            _recorder.RecordAppliedSeedBatches([batch], catalogVersion);
            results[^1] = SeedFixtureBatchApplyResult.Applied(
                batch,
                "Mismatched recorded metadata was allowed by policy, so seed SQL was executed and metadata was replaced.");
        }

        return new SeedFixtureBatchApplyReport(
            results,
            HadBlockedBatch: results.Any(static result => result.Status == SeedFixtureBatchApplyStatus.Blocked));
    }

    private static bool Matches(SeedFixtureBatchRecord existing, SeedFixtureBatch batch) =>
        existing.TableName.Equals(batch.TableName, StringComparison.OrdinalIgnoreCase) &&
        existing.SourceName.Equals(batch.SourceName, StringComparison.Ordinal) &&
        existing.Scenario.Equals(batch.Scenario, StringComparison.Ordinal) &&
        existing.RowCount == batch.RowCount &&
        existing.ContentHash.Equals(batch.ContentHash, StringComparison.OrdinalIgnoreCase);
}

public sealed record SeedFixtureBatchApplyReport(
    IReadOnlyList<SeedFixtureBatchApplyResult> Results,
    bool HadBlockedBatch)
{
    public int AppliedCount => Results.Count(static result => result.Status == SeedFixtureBatchApplyStatus.Applied);
    public int SkippedCount => Results.Count(static result => result.Status == SeedFixtureBatchApplyStatus.Skipped);
    public int BlockedCount => Results.Count(static result => result.Status == SeedFixtureBatchApplyStatus.Blocked);
}

public sealed record SeedFixtureBatchApplyResult(
    string BatchId,
    string TableName,
    SeedFixtureBatchApplyStatus Status,
    string Message)
{
    public static SeedFixtureBatchApplyResult Applied(
        SeedFixtureBatch batch,
        string message = "Seed batch SQL was executed and metadata was recorded.") =>
        new(batch.BatchId, batch.TableName, SeedFixtureBatchApplyStatus.Applied, message);

    public static SeedFixtureBatchApplyResult Skipped(
        SeedFixtureBatch batch,
        string message) =>
        new(batch.BatchId, batch.TableName, SeedFixtureBatchApplyStatus.Skipped, message);

    public static SeedFixtureBatchApplyResult Blocked(
        SeedFixtureBatch batch,
        string message) =>
        new(batch.BatchId, batch.TableName, SeedFixtureBatchApplyStatus.Blocked, message);
}

public sealed class SeedFixtureBatchMismatchException : InvalidOperationException
{
    public SeedFixtureBatchMismatchException(SeedFixtureBatchApplyReport report)
        : base("One or more seed fixture batches did not match recorded metadata.")
    {
        Report = report;
    }

    public SeedFixtureBatchApplyReport Report { get; }
}