using DeltaZulu.Platform.Data.Proton.Ddl;
using DeltaZulu.Platform.Data.Proton.Sql;
using DeltaZulu.Platform.Domain.Analytics.Detection;
using DeltaZulu.Platform.Domain.Analytics.QueryModel;

namespace DeltaZulu.Platform.Data.Proton;

/// <summary>
/// Timeplus Proton implementation of <see cref="IDetectionCompilationBackend"/>.
/// Produces Proton-specific SQL and DDL for NRT (materialized view + alert) and scheduled
/// (task) detections. Application-layer orchestrators depend only on the domain port.
/// </summary>
public sealed class ProtonDetectionCompilationBackend : IDetectionCompilationBackend
{
    private readonly ProtonSqlQueryEmitter _emitter = new();

    public string EmitSelectSql(RelNode query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _emitter.Emit(query).Sql;
    }

    // -------------------------------------------------------------------------
    // NRT
    // -------------------------------------------------------------------------

    public string BuildNrtDeploymentDdl(string ruleId, string selectSql)
    {
        ValidateRuleId(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectSql);
        return new MaterializedViewDdl($"mv_nrt_{ruleId}")
            .As(selectSql)
            .Build();
    }

    public string BuildNrtAlertDdl(
        string ruleId,
        string udfName,
        int batchEvents,
        TimeSpan batchTimeout,
        int? limitAlerts = null,
        TimeSpan? limitPer = null)
    {
        ValidateRuleId(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(udfName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchEvents);
        if (batchTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(batchTimeout), "Batch timeout must be positive.");

        var alert = new AlertDdl($"alert_nrt_{ruleId}")
            .BatchEvents(batchEvents, ToProtonInterval(batchTimeout))
            .Call(udfName)
            .As($"SELECT * FROM mv_nrt_{ruleId}");

        if (limitAlerts.HasValue && limitPer.HasValue)
        {
            if (limitAlerts.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(limitAlerts), "Limit alerts must be positive.");
            if (limitPer.Value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(limitPer), "Limit per must be positive.");
            alert.LimitAlerts(limitAlerts.Value, ToProtonInterval(limitPer.Value));
        }

        return alert.Build();
    }

    public string BuildDropNrtDdl(string ruleId)
    {
        ValidateRuleId(ruleId);
        var dropMv    = new MaterializedViewDdl($"mv_nrt_{ruleId}").BuildDrop();
        var dropAlert = new AlertDdl($"alert_nrt_{ruleId}").BuildDrop();
        return dropAlert + Environment.NewLine + dropMv;
    }

    // -------------------------------------------------------------------------
    // Scheduled
    // -------------------------------------------------------------------------

    public string BuildScheduledDeploymentDdl(
        string ruleId,
        string selectSql,
        TimeSpan schedule,
        TimeSpan timeout,
        string targetStream)
    {
        ValidateRuleId(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectSql);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetStream);
        if (schedule <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(schedule), "Schedule must be positive.");
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");

        return new ScheduledTaskDdl($"sched_{ruleId}")
            .Schedule(ToProtonInterval(schedule))
            .Timeout(ToProtonInterval(timeout))
            .Into(targetStream)
            .As(selectSql)
            .Build();
    }

    public string BuildDropScheduledDdl(string ruleId)
    {
        ValidateRuleId(ruleId);
        return new ScheduledTaskDdl($"sched_{ruleId}").BuildDrop();
    }

    // -------------------------------------------------------------------------

    private static void ValidateRuleId(string ruleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        if (!ruleId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            throw new ArgumentException($"Rule ID contains invalid characters: '{ruleId}'", nameof(ruleId));
    }

    private static ProtonInterval ToProtonInterval(TimeSpan ts)
    {
        // Map to the most appropriate Proton interval unit (largest whole unit that fits).
        if (ts.TotalSeconds < 60) return ProtonInterval.Seconds(Math.Max(1, (int)ts.TotalSeconds));
        if (ts.TotalMinutes < 60) return ProtonInterval.Minutes((int)ts.TotalMinutes);
        if (ts.TotalHours < 24)   return ProtonInterval.Hours((int)ts.TotalHours);
        if (ts.TotalDays < 7)     return ProtonInterval.Days((int)ts.TotalDays);
        return ProtonInterval.Weeks((int)(ts.TotalDays / 7));
    }
}
