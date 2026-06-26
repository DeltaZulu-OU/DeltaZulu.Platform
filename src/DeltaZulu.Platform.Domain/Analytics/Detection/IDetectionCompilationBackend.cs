using DeltaZulu.Platform.Domain.Analytics.QueryModel;

namespace DeltaZulu.Platform.Domain.Analytics.Detection;

/// <summary>
/// Backend-neutral port for compiling a RelNode query into detection deployment artifacts.
/// Application-layer orchestrators depend on this interface; the concrete implementation is
/// infrastructure-specific and lives in the Data layer.
/// </summary>
public interface IDetectionCompilationBackend
{
    /// <summary>
    /// Emits a streaming SELECT statement from the relational query model.
    /// Throws <see cref="NotSupportedException"/> if the query contains constructs the backend
    /// cannot translate.
    /// </summary>
    string EmitSelectSql(RelNode query);

    // -------------------------------------------------------------------------
    // NRT (Materialized View + Alert)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Wraps <paramref name="selectSql"/> in a <c>CREATE MATERIALIZED VIEW</c> statement for
    /// continuous NRT detection against the Gold streams.
    /// </summary>
    string BuildNrtDeploymentDdl(string ruleId, string selectSql);

    /// <summary>
    /// Builds the backend-specific NRT alert DDL that continuously monitors
    /// <paramref name="selectSql"/> and invokes <paramref name="udfName"/> when
    /// <paramref name="batchEvents"/> events accumulate or <paramref name="batchTimeout"/> elapses,
    /// whichever comes first.
    /// </summary>
    string BuildNrtAlertDdl(string ruleId, string selectSql, string udfName, int batchEvents, TimeSpan batchTimeout);

    /// <summary>
    /// Builds a <c>CREATE ALERT</c> statement that monitors the NRT materialized view for
    /// <paramref name="ruleId"/> and invokes <paramref name="udfName"/> when the batch threshold
    /// is met. The UDF is responsible for writing to the <c>alert_dispatch</c> stream.
    /// </summary>
    string BuildNrtAlertDdl(
        string ruleId,
        string udfName,
        int batchEvents,
        TimeSpan batchTimeout,
        int? limitAlerts = null,
        TimeSpan? limitPer = null);

    /// <summary>
    /// Returns DDL that drops both the NRT materialized view and its associated alert.
    /// </summary>
    string BuildDropNrtDdl(string ruleId);

    // -------------------------------------------------------------------------
    // Scheduled (Task)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a <c>CREATE OR REPLACE TASK</c> statement that runs <paramref name="selectSql"/>
    /// on the given <paramref name="schedule"/> and writes results to <paramref name="targetStream"/>.
    /// </summary>
    string BuildScheduledDeploymentDdl(
        string ruleId,
        string selectSql,
        TimeSpan schedule,
        TimeSpan timeout,
        string targetStream);

    /// <summary>Returns DDL that drops the scheduled task.</summary>
    string BuildDropScheduledDdl(string ruleId);
}
