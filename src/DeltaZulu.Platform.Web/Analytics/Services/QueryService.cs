using DeltaZulu.Platform.Data.Analytics;
using DeltaZulu.Platform.Data.DuckDb;
using DeltaZulu.Platform.Domain.Analytics.Execution;
using DeltaZulu.Platform.Domain.Analytics.QueryHistory;
using DeltaZulu.Platform.Web.Analytics.Rendering;

namespace DeltaZulu.Platform.Web.Analytics.Services;

public sealed partial class QueryService(
    IAnalyticsQueryExecutor executor,
    ILogger<QueryService> logger,
    IQueryHistoryRepository queryHistory) : IDataOnlyQueryService
{
    public Task<QueryResult> ExecuteAsync(
        string kql,
        CancellationToken ct = default)
        => ExecuteCoreAsync(kql, ExecutionPurpose.Interactive, ct);

    public Task<QueryResult> ExecuteDataOnlyAsync(
        string kql,
        CancellationToken ct = default)
        => ExecuteCoreAsync(kql, ExecutionPurpose.Dashboard, ct);

    private static QueryResult ToQueryResult(AnalyticsQueryResult result)
    {
        if (!result.Success)
        {
            return QueryResult.FromDiagnostics(
                result.Diagnostics,
                result.DebugTrace.Count > 0 ? [.. result.DebugTrace] : null);
        }

        var columnData = new List<object?>[result.ColumnData.Count];
        for (var i = 0; i < result.ColumnData.Count; i++)
        {
            columnData[i] = [.. result.ColumnData[i]];
        }

        return QueryResult.FromData(
            [.. result.Columns.Select(static c => new ResultColumn(c.Name, c.TypeName))],
            columnData,
            result.GeneratedSql,
            result.PlannerStatsJson,
            result.SqlShapeStatsJson,
            result.DebugTrace.Count > 0 ? [.. result.DebugTrace] : null,
            result.Diagnostics);
    }

    private async Task<QueryResult> ExecuteCoreAsync(
        string kql,
        ExecutionPurpose purpose,
        CancellationToken ct)
    {
        var startedAt = DateTime.UtcNow;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var executionResult = await executor.ExecuteAsync(
            new AnalyticsQueryRequest(kql, purpose),
            ct);
        var result = ToQueryResult(executionResult);

        sw.Stop();
        await TryRecordHistoryAsync(kql, startedAt, sw.ElapsedMilliseconds, result, ct);

        return result;
    }

    private async Task TryRecordHistoryAsync(
        string kql, DateTime startedAt, long durationMs,
        QueryResult result, CancellationToken ct)
    {
        try
        {
            var diagnosticSummary = result.Diagnostics.All.Count == 0
                ? null
                : string.Join(" | ", result.Diagnostics.All.Select(d => d.Message));

            await queryHistory.AddAsync(
                new QueryHistoryRecord(
                    Guid.NewGuid().ToString("N"), kql, startedAt,
                    result.Success,
                    result.Success ? result.RowCount : null,
                    durationMs, diagnosticSummary), ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            LogQueryHistoryFailure(ex);
        }
    }

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning,
        Message = "Failed to record query history.")]
    private partial void LogQueryHistoryFailure(Exception ex);
}