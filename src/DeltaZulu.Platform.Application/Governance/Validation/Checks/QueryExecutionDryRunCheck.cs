using System.Text.Json;
using DeltaZulu.Platform.Domain.Analytics.Execution;
using DeltaZulu.Platform.Domain.Governance.Contracts;
using DeltaZulu.Platform.Domain.Governance.Enums;
using Microsoft.Extensions.Logging;

namespace DeltaZulu.Platform.Application.Governance.Validation.Checks;

public sealed partial class QueryExecutionDryRunCheck(
    IAnalyticsQueryExecutor executor,
    ILogger<QueryExecutionDryRunCheck> logger) : ICheck
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name => "query-execution-dry-run";
    public bool IsBlocking => false;

    public IReadOnlySet<DraftContentType> ApplicableContentTypes { get; } =
        new HashSet<DraftContentType> { DraftContentType.AnalyticsQuery }.AsReadOnly();

    public async Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var queries = context.DraftFiles
            .Where(f => f.ContentType == DraftContentType.AnalyticsQuery)
            .ToList();

        if (queries.Count == 0)
        {
            return CheckOutcome.Skip("No query files in draft set.");
        }

        var failures = new List<QueryDryRunFailure>();
        var succeeded = 0;

        foreach (var query in queries)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(query.Content))
            {
                failures.Add(new QueryDryRunFailure(query.LogicalPath, "Empty query content."));
                continue;
            }

            try
            {
                var request = AnalyticsQueryRequest.ValidationDryRun(query.Content);
                var result = await executor.ExecuteAsync(request, ct);

                if (result.Success)
                {
                    succeeded++;
                    LogDryRunPassed(logger, query.LogicalPath, result.RowCount);
                }
                else
                {
                    var summary = result.Diagnostics.HasErrors
                        ? string.Join("; ", result.Diagnostics.Errors.Select(d => d.Message))
                        : "Execution failed without diagnostics.";
                    failures.Add(new QueryDryRunFailure(query.LogicalPath, summary));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogDryRunException(logger, ex, query.LogicalPath);
                failures.Add(new QueryDryRunFailure(
                    query.LogicalPath,
                    $"Dry-run error: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        if (failures.Count == 0)
        {
            return CheckOutcome.Pass(
                $"Dry-run execution passed ({succeeded} query file(s)).");
        }

        var logs = string.Join('\n', failures.Select(f => $"{f.LogicalPath}: {f.Reason}"));
        var details = JsonSerializer.Serialize(new { failures }, JsonOptions);

        return CheckOutcome.Fail(
            $"{failures.Count} query dry-run failure(s) out of {queries.Count} file(s).",
            details,
            logs);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Dry-run passed for {LogicalPath}: {RowCount} row(s).")]
    private static partial void LogDryRunPassed(ILogger logger, string logicalPath, int rowCount);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Dry-run threw an exception for {LogicalPath}.")]
    private static partial void LogDryRunException(ILogger logger, Exception ex, string logicalPath);

    private sealed record QueryDryRunFailure(string LogicalPath, string Reason);
}