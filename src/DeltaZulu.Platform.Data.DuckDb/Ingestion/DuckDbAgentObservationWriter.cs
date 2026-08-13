using System.Text;
using Dapper;
using DeltaZulu.Platform.Domain.Analytics.Observability;

namespace DeltaZulu.Platform.Data.DuckDb.Ingestion;

public sealed class DuckDbAgentObservationWriter(SchemaApplier applier)
    : DuckDbAppendOnlyWriterBase<AgentObservationSnapshot>(applier)
{
    private const string TableName = "internal.AgentObservations";
    private const string ColumnList = "ObservedAt, TenantId, AgentId, HostId, Hostname, Platform, AgentVersion, LastSeenAt, IsEnabled, ReportedStatus, BufferPressure, QueueDepth, DroppedCount, ForwardFailedCount, DesiredConfigVersionId, AppliedConfigVersionId, DesiredProfileVersionId, AppliedProfileVersionId";

    protected override (string Sql, DynamicParameters Parameters) BuildInsert(AgentObservationSnapshot s)
    {
        var sb = new StringBuilder(768);
        var parameters = new DynamicParameters();
        var index = 0;

        sb.Append($"INSERT INTO {TableName} ({ColumnList}) VALUES ");
        AppendRowPlaceholders(sb, parameters, ref index, RowValues(s));
        sb.Append(';');

        return (sb.ToString(), parameters);
    }

    protected override (string Sql, DynamicParameters Parameters) BuildBatchInsert(IReadOnlyList<AgentObservationSnapshot> snapshots)
    {
        var sb = new StringBuilder(256 + snapshots.Count * 384);
        var parameters = new DynamicParameters();
        var index = 0;

        sb.AppendLine($"INSERT INTO {TableName} ({ColumnList}) VALUES");
        for (var i = 0; i < snapshots.Count; i++)
        {
            if (i > 0) sb.AppendLine(",");
            AppendRowPlaceholders(sb, parameters, ref index, RowValues(snapshots[i]));
        }
        sb.Append(';');

        return (sb.ToString(), parameters);
    }

    private static object?[] RowValues(AgentObservationSnapshot s) =>
    [
        s.ObservedAtUtc,
        s.TenantId,
        s.AgentId,
        s.HostId,
        s.Hostname,
        s.Platform,
        s.AgentVersion,
        s.LastSeenAtUtc,
        s.IsEnabled,
        s.ReportedStatus,
        s.BufferPressure,
        s.QueueDepth,
        s.DroppedCount,
        s.ForwardFailedCount,
        NullIfEmpty(s.DesiredConfigVersionId),
        NullIfEmpty(s.AppliedConfigVersionId),
        NullIfEmpty(s.DesiredProfileVersionId),
        NullIfEmpty(s.AppliedProfileVersionId),
    ];
}
