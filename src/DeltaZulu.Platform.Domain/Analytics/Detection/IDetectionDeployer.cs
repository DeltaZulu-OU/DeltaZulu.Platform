namespace DeltaZulu.Platform.Domain.Analytics.Detection;

/// <summary>
/// Executes compiled detection DDL against the streaming engine and manages artifact lifecycle.
/// For NRT detections, deploys both the materialized view and the ALERT as an atomic unit.
/// For scheduled detections, deploys the task definition.
/// </summary>
public interface IDetectionDeployer
{
    /// <summary>
    /// Deploys an NRT detection: executes <paramref name="mvDdl"/> then <paramref name="alertDdl"/>
    /// against the streaming engine. Both artifacts are created together; retract removes both.
    /// </summary>
    Task DeployNrtAsync(string ruleId, string mvDdl, string alertDdl, CancellationToken ct = default);

    /// <summary>
    /// Retracts an NRT detection by dropping both its materialized view and its alert.
    /// </summary>
    Task RetractNrtAsync(string ruleId, CancellationToken ct = default);

    /// <summary>Deploys a scheduled task detection.</summary>
    Task DeployScheduledAsync(string ruleId, string taskDdl, CancellationToken ct = default);

    /// <summary>Retracts a scheduled task detection by dropping its task.</summary>
    Task RetractScheduledAsync(string ruleId, CancellationToken ct = default);
}
