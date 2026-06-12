namespace DeltaZulu.Platform.Application.Analytics.Execution;

public interface IAnalyticsQueryExecutor
{
    Task<AnalyticsQueryResult> ExecuteAsync(
        AnalyticsQueryRequest request,
        CancellationToken cancellationToken = default);
}
