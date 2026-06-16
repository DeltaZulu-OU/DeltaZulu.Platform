namespace DeltaZulu.Platform.Domain.Analytics.Execution;

public interface IAnalyticsQueryExecutor
{
    Task<AnalyticsQueryResult> ExecuteAsync(
        AnalyticsQueryRequest request,
        CancellationToken cancellationToken = default);
}