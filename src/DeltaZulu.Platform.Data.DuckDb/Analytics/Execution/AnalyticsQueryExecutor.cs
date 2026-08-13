using DeltaZulu.Platform.Domain.Analytics.Execution;
using DeltaZulu.Platform.Domain.Analytics.Policy;
using Microsoft.Extensions.Logging;

namespace DeltaZulu.Platform.Data.DuckDb.Execution;

/// <summary>
/// Shared Analytics execution adapter over the DuckDB runtime. It owns the single
/// serialization point for the current DuckDB connection model and applies
/// purpose-specific materialization policy before UI, governance, and future
/// operations callers adapt the result to their own workflows.
/// </summary>
public sealed partial class AnalyticsQueryExecutor : IAnalyticsQueryExecutor, IDisposable
{
    private readonly ILogger<AnalyticsQueryExecutor> _logger;
    private readonly QueryRuntime _runtime;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public AnalyticsQueryExecutor(
        QueryRuntime runtime,
        ILogger<AnalyticsQueryExecutor> logger)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Dispose() => _semaphore.Dispose();

    public async Task<AnalyticsQueryResult> ExecuteAsync(
        AnalyticsQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.QueryText);

        var maxRows = ResolveMaxRows(request);

        try
        {
            await _semaphore.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            var bag = new DiagnosticBag();
            bag.AddError(DiagnosticPhase.Execute,
                "Query was cancelled before execution started.");
            return FromDiagnostics(bag);
        }

        try
        {
            List<object?>[]? columnData = null;
            var rowCount = 0;
            var streamResult = await Task.Run(() => _runtime.ExecuteStreamedDataOnly(
                request.QueryText,
                reader => {
                    if (rowCount >= maxRows)
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

            if (!streamResult.Success)
            {
                LogQueryFailed(
                    request.Purpose,
                    request.QueryText,
                    streamResult.GeneratedSql,
                    string.Join(" | ", streamResult.Diagnostics.All.Select(d => d.Message)),
                    string.Join(" | ", streamResult.DebugTrace));

                return FromDiagnostics(
                    streamResult.Diagnostics,
                    streamResult.DebugTrace.Count > 0 ? [.. streamResult.DebugTrace] : null);
            }

            var trace = streamResult.DebugTrace.Count > 0 ? new List<string>(streamResult.DebugTrace) : [];
            if (streamResult.RowCount > rowCount)
            {
                trace.Add($"Materialization truncated at {maxRows} rows for {request.Purpose} execution policy.");
            }

            var result = FromData(
                [.. streamResult.Columns.Select(static c => new AnalyticsResultColumn(c.Name, c.TypeName))],
                columnData ?? CreateColumnData(streamResult.Columns.Count),
                streamResult.GeneratedSql,
                streamResult.PlannerStatsJson,
                streamResult.PlannerMermaid,
                streamResult.SqlShapeStatsJson,
                trace,
                streamResult.Diagnostics);

            if (result.DebugTrace.Count > 0)
            {
                LogQueryDebugTrace(
                    request.Purpose,
                    result.DebugTrace.Count,
                    result.Success,
                    string.Join(" | ", result.DebugTrace));
            }

            return result;
        }
        catch (Exception ex)
        {
            LogUnhandledKqlError(ex, request.Purpose, request.QueryText);
            var bag = new DiagnosticBag();
            bag.AddError(DiagnosticPhase.Execute,
                "An unexpected error occurred. Check application logs.");

            return FromDiagnostics(bag);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static int ResolveMaxRows(AnalyticsQueryRequest request)
    {
        if (request.MaxRows is null)
        {
            return AnalyticsExecutionPolicies.GetMaxMaterializedRows(request.Purpose);
        }

        if (request.MaxRows.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.MaxRows.Value,
                "Maximum materialized rows must be greater than zero.");
        }

        var purposeLimit = AnalyticsExecutionPolicies.GetMaxMaterializedRows(request.Purpose);
        return Math.Min(request.MaxRows.Value, purposeLimit);
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

    private static AnalyticsQueryResult FromData(
        List<AnalyticsResultColumn> columns,
        List<object?>[] columnData,
        string? sql,
        string? plannerStatsJson,
        string? plannerMermaid,
        string? sqlShapeStatsJson,
        List<string>? debugTrace,
        DiagnosticBag diagnostics)
    {
        var readonlyColumnData = new IReadOnlyList<object?>[columnData.Length];
        for (var i = 0; i < columnData.Length; i++)
        {
            readonlyColumnData[i] = columnData[i];
        }

        return new AnalyticsQueryResult {
            Success = true,
            Columns = columns,
            ColumnData = readonlyColumnData,
            GeneratedSql = sql,
            PlannerStatsJson = plannerStatsJson,
            PlannerMermaid = plannerMermaid,
            SqlShapeStatsJson = sqlShapeStatsJson,
            DebugTrace = debugTrace ?? [],
            Diagnostics = diagnostics
        };
    }

    private static AnalyticsQueryResult FromDiagnostics(
        DiagnosticBag diagnostics,
        List<string>? debugTrace = null) => new() {
            Success = false,
            DebugTrace = debugTrace ?? [],
            Diagnostics = diagnostics
        };

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Query failed for {Purpose}. KQL: {Kql}. SQL: {GeneratedSql}. Diagnostics: {Diagnostics}. Trace: {Trace}")]
    private partial void LogQueryFailed(ExecutionPurpose purpose, string kql, string? generatedSql, string diagnostics, string trace);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Query debug trace for {Purpose} ({TraceCount} events, success={Success}): {DebugTrace}")]
    private partial void LogQueryDebugTrace(ExecutionPurpose purpose, int traceCount, bool success, string debugTrace);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error,
        Message = "Unhandled error executing {Purpose} KQL: {Kql}")]
    private partial void LogUnhandledKqlError(Exception ex, ExecutionPurpose purpose, string kql);
}