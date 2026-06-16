namespace DeltaZulu.Platform.Domain.Analytics.Execution;

public enum ExecutionPurpose
{
    Interactive = 0,
    Dashboard = 1,
    ValidationDryRun = 2,
    ScheduledDetection = 3,
    Recovery = 4
}