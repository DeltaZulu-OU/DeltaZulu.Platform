namespace DeltaZulu.Platform.Application.Analytics.Execution;

/// <summary>
/// Identifies the caller intent for an analytics query so execution policy can be
/// centralized instead of recreated by each UI, governance, or operations path.
/// </summary>
public enum ExecutionPurpose
{
    Interactive = 0,
    Dashboard = 1,
    ValidationDryRun = 2,
    ScheduledDetection = 3,
    Recovery = 4
}
