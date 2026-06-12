namespace DeltaZulu.Platform.Application.Analytics.Execution;

public static class AnalyticsExecutionPolicies
{
    public const int InteractiveMaxMaterializedRows = 2_000;
    public const int DashboardMaxMaterializedRows = 2_000;
    public const int ValidationDryRunMaxMaterializedRows = 100;
    public const int ScheduledDetectionMaxMaterializedRows = 10_000;
    public const int RecoveryMaxMaterializedRows = 10_000;

    public static int GetMaxMaterializedRows(ExecutionPurpose purpose) => purpose switch
    {
        ExecutionPurpose.Interactive => InteractiveMaxMaterializedRows,
        ExecutionPurpose.Dashboard => DashboardMaxMaterializedRows,
        ExecutionPurpose.ValidationDryRun => ValidationDryRunMaxMaterializedRows,
        ExecutionPurpose.ScheduledDetection => ScheduledDetectionMaxMaterializedRows,
        ExecutionPurpose.Recovery => RecoveryMaxMaterializedRows,
        _ => InteractiveMaxMaterializedRows
    };
}
