namespace Hunting.Web.Services;

using DuckDB.NET.Data;
using Hunting.Core.Policy;
using Hunting.Data;
using Hunting.Data.QueryHistory;
using Hunting.Web.Rendering;

/// <summary>
/// <para>
/// Wraps QueryRuntime with a SemaphoreSlim to serialize DuckDB access
/// across concurrent Blazor Server circuits.
/// </para>
/// <para>
/// The single-connection MVP model is correct but not concurrent-safe.
/// All query execution goes through this service so the serialization
/// point is explicit and named, not implicit.
/// </para>
/// </summary>
public sealed class QueryService : IDataOnlyQueryService, IDisposable
{
    private const int MaxMaterializedRows = 2000;
    private readonly IQueryHistoryRepository _queryHistory;
    private readonly ILogger<QueryService> _logger;
    private readonly QueryRuntime _runtime;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public QueryService(
        QueryRuntime runtime,
        ILogger<QueryService> logger,
        IQueryHistoryRepository queryHistory)
    {
        _runtime = runtime;
        _logger = logger;
        _queryHistory = queryHistory;
    }

    public void Dispose() => _semaphore.Dispose();

    /// <summary>
    /// Execute a KQL query through the legacy UI path. This preserves the current
    /// compatibility behavior where QueryRuntime strips a terminal render directive
    /// and exposes the legacy RenderSpec on the result.
    /// </summary>
    public Task<QueryResult> ExecuteAsync(
        string kql,
        CancellationToken ct = default)
        => ExecuteCoreAsync(kql, _runtime.ExecuteStreamed, ct);

    /// <summary>
    /// Execute a KQL query as data only. This path deliberately does not strip or
    /// interpret render directives; callers that need rendering must parse render
    /// outside the runtime and pass only the stripped data query here.
    /// </summary>
    public Task<QueryResult> ExecuteDataOnlyAsync(
        string kql,
        CancellationToken ct = default)
        => ExecuteCoreAsync(kql, _runtime.ExecuteStreamedDataOnly, ct);

    /// <summary>
    /// Execute a KQL query. Serializes access to the single DuckDB connection.
    /// Cancellation token is respected for the wait; query execution itself uses
    /// the runtime's configured timeout.
    /// </summary>
    private async Task<QueryResult> ExecuteCoreAsync(
        string kql,
        Func<string, Func<DuckDBDataReader, bool>, QueryStreamResult> execute,
        CancellationToken ct)
    {
        // The cancellation token cancels the wait for the connection only. If the
        // wait is cancelled we never acquired the semaphore, so there is nothing to
        // release — return a diagnostic without entering the protected region.
        try
        {
            await _semaphore.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            var bag = new DiagnosticBag();
            bag.AddError(DiagnosticPhase.Execute,
                "Query was cancelled before execution started.");
            return QueryResult.FromDiagnostics(bag);
        }

        var startedAt = DateTime.UtcNow;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Deliberately NOT passing ct to Task.Run: cancelling it would abandon
            // the task and release the semaphore while the DuckDB delegate is still
            // running on the single shared connection, letting a second query in
            // concurrently. A started query runs to completion (or its own
            // CommandTimeout); the semaphore is released only once it truly ends.
            List<object?>[]? columnData = null;
            var rowCount = 0;
            var streamResult = await Task.Run(() => execute(kql, reader => {
                if (rowCount >= MaxMaterializedRows)
                {
                    return false;
                }

                columnData ??= CreateColumnData(reader.FieldCount);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    columnData[i].Add(DuckDbValueReader.ReadValue(reader, i));
                }

                rowCount++;
                return true;
            }));

            QueryResult result;
            if (!streamResult.Success)
            {
                _logger.LogWarning(
                    "Query failed. KQL: {Kql}. SQL: {GeneratedSql}. Diagnostics: {Diagnostics}. Trace: {Trace}",
                    kql,
                    streamResult.GeneratedSql,
                    string.Join(" | ", streamResult.Diagnostics.All.Select(d => d.Message)),
                    string.Join(" | ", streamResult.DebugTrace));

                result = QueryResult.FromDiagnostics(
                    streamResult.Diagnostics,
                    streamResult.DebugTrace.Count > 0 ? [.. streamResult.DebugTrace] : null);
            }
            else
            {
                var trace = streamResult.DebugTrace.Count > 0 ? new List<string>(streamResult.DebugTrace) : [];
                if (streamResult.RowCount > rowCount)
                {
                    trace.Add($"Materialization truncated at {MaxMaterializedRows} rows for UI safety.");
                }

                result = QueryResult.FromData(
                    [.. streamResult.Columns],
                    columnData ?? CreateColumnData(streamResult.Columns.Count),
                    streamResult.GeneratedSql,
                    streamResult.PlannerStatsJson,
                    streamResult.SqlShapeStatsJson,
                    trace,
                    streamResult.Diagnostics);
            }

            if (result.DebugTrace.Count > 0)
            {
                _logger.LogDebug(
                    "Query debug trace ({TraceCount} events, success={Success}): {DebugTrace}",
                    result.DebugTrace.Count,
                    result.Success,
                    string.Join(" | ", result.DebugTrace));
            }

            sw.Stop();
            await RecordQueryHistoryAsync(kql, startedAt, sw.ElapsedMilliseconds, result, ct);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error executing KQL: {Kql}", kql);
            var bag = new DiagnosticBag();
            bag.AddError(DiagnosticPhase.Execute,
                "An unexpected error occurred. Check application logs.");

            var result = QueryResult.FromDiagnostics(bag);
            sw.Stop();
            await RecordQueryHistoryAsync(kql, startedAt, sw.ElapsedMilliseconds, result, ct);

            return result;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task RecordQueryHistoryAsync(
        string kql,
        DateTime startedAt,
        long durationMs,
        QueryResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            var diagnosticSummary = result.Diagnostics.All.Count == 0
                ? null
                : string.Join(" | ", result.Diagnostics.All.Select(d => d.Message));

            await _queryHistory.AddAsync(
                new QueryHistoryRecord(
                    Guid.NewGuid().ToString("N"),
                    kql,
                    startedAt,
                    result.Success,
                    result.Success ? result.RowCount : null,
                    durationMs,
                    diagnosticSummary),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record query history.");
        }
    }

    private static List<object?>[] CreateColumnData(int count)
    {
        var columns = new List<object?>[count];
        for (var i = 0; i < count; i++)
        {
            columns[i] = [];
        }

        return columns;
    }
}
