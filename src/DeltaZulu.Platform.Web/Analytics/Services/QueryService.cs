
using DeltaZulu.Platform.Application.Analytics.Execution;
using DeltaZulu.Platform.Data.Analytics;
using DeltaZulu.Platform.Data.DuckDb;
using DeltaZulu.Platform.Domain.Analytics.Policy;
using DeltaZulu.Platform.Domain.Analytics.QueryHistory;
using DeltaZulu.Platform.Web.Analytics.Rendering;

namespace DeltaZulu.Platform.Web.Analytics.Services;
/// <summary>
/// <para>
/// Adapts the shared application-layer analytics executor for Blazor callers.
/// </para>
/// <para>
/// Query execution policy and DuckDB serialization live behind
/// <see cref="IAnalyticsQueryExecutor" />. This web adapter keeps UI result
/// shape compatibility and records query history.
/// </para>
/// </summary>
public sealed partial class QueryService : IDataOnlyQueryService
{
    private readonly IAnalyticsQueryExecutor _executor;
    private readonly ILogger<QueryService> _logger;
    private readonly IQueryHistoryRepository _queryHistory;

    public QueryService(
        IAnalyticsQueryExecutor executor,
        ILogger<QueryService> logger,
        IQueryHistoryRepository queryHistory)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _queryHistory = queryHistory ?? throw new ArgumentNullException(nameof(queryHistory));
    }

    /// <summary>
    /// Execute a KQL query through the standard data path.
    /// Rendering is handled outside QueryRuntime by the Web rendering pipeline.
    /// </summary>
    public Task<QueryResult> ExecuteAsync(
        string kql,
        CancellationToken ct = default)
        => ExecuteCoreAsync(kql, ExecutionPurpose.Interactive, ct);

    /// <summary>
    /// Execute a KQL query as data only. This path deliberately does not strip or
    /// interpret render directives; callers that need rendering must parse render
    /// outside the runtime and pass only the stripped data query here.
    /// </summary>
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

    /// <summary>
    /// Execute a KQL query through the shared application-layer executor and
    /// preserve web-specific query-history behavior in this adapter.
    /// </summary>
    private async Task<QueryResult> ExecuteCoreAsync(
        string kql,
        ExecutionPurpose purpose,
        CancellationToken ct)
    {
        var startedAt = DateTime.UtcNow;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var executionResult = await _executor.ExecuteAsync(
            new AnalyticsQueryRequest(kql, purpose),
            ct);
        var result = ToQueryResult(executionResult);

        sw.Stop();
        await RecordQueryHistoryAsync(kql, startedAt, sw.ElapsedMilliseconds, result, ct);

        return result;
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
            LogQueryHistoryFailure(ex);
        }
    }

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning,
        Message = "Failed to record query history.")]
    private partial void LogQueryHistoryFailure(Exception ex);
}