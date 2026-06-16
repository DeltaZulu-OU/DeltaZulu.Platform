namespace DeltaZulu.Platform.Domain.Analytics.Execution;

public sealed record AnalyticsQueryRequest(
    string QueryText,
    ExecutionPurpose Purpose,
    int? MaxRows = null)
{
    public static AnalyticsQueryRequest Interactive(string queryText, int? maxRows = null)
        => new(queryText, ExecutionPurpose.Interactive, maxRows);

    public static AnalyticsQueryRequest Dashboard(string queryText, int? maxRows = null)
        => new(queryText, ExecutionPurpose.Dashboard, maxRows);

    public static AnalyticsQueryRequest ValidationDryRun(string queryText, int? maxRows = null)
        => new(queryText, ExecutionPurpose.ValidationDryRun, maxRows);
}
