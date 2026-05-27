namespace Hunting.Web.Services;

using Hunting.Core.Policy;
using Hunting.Data;

/// <summary>
/// Wraps QueryRuntime with a SemaphoreSlim to serialize DuckDB access
/// across concurrent Blazor Server circuits.
///
/// The single-connection MVP model is correct but not concurrent-safe.
/// All query execution goes through this service so the serialization
/// point is explicit and named, not implicit.
/// </summary>
public sealed class QueryService : IDisposable
{
    private readonly QueryRuntime _runtime;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger<QueryService> _logger;
    private const int MaxMaterializedRows = 2000;

    public QueryService(QueryRuntime runtime, ILogger<QueryService> logger)
    {
        _runtime = runtime;
        _logger = logger;
    }

    /// <summary>
    /// Execute a KQL query. Serializes access to the single DuckDB connection.
    /// Cancellation token is respected for the wait; query execution itself uses
    /// the runtime's configured timeout.
    /// </summary>
    public async Task<QueryResult> ExecuteAsync(
        string kql,
        CancellationToken ct = default)
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

        try
        {
            // Deliberately NOT passing ct to Task.Run: cancelling it would abandon
            // the task and release the semaphore while the DuckDB delegate is still
            // running on the single shared connection, letting a second query in
            // concurrently. A started query runs to completion (or its own
            // CommandTimeout); the semaphore is released only once it truly ends.
            List<object?>[]? columnData = null;
            var rowCount = 0;
            var streamResult = await Task.Run(() => _runtime.ExecuteStreamed(kql, reader =>
            {
                if (rowCount >= MaxMaterializedRows)
                {
                    return false;
                }

                columnData ??= CreateColumnData(reader.FieldCount);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    columnData[i].Add(reader.IsDBNull(i) ? null : reader.GetValue(i));
                }
                rowCount++;
                return true;
            }));

            QueryResult result;
            if (!streamResult.Success)
            {
                result = QueryResult.FromDiagnostics(streamResult.Diagnostics, streamResult.DebugTrace.Count > 0 ? [..streamResult.DebugTrace] : null);
            }
            else
            {
                var trace = streamResult.DebugTrace.Count > 0 ? new List<string>(streamResult.DebugTrace) : [];
                if (streamResult.RowCount > rowCount)
                {
                    trace.Add($"Materialization truncated at {MaxMaterializedRows} rows for UI safety.");
                }

                result = QueryResult.FromData(
                    [..streamResult.Columns],
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

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error executing KQL: {Kql}", kql);
            var bag = new DiagnosticBag();
            bag.AddError(DiagnosticPhase.Execute,
                "An unexpected error occurred. Check application logs.");
            return QueryResult.FromDiagnostics(bag);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose() => _semaphore.Dispose();

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
