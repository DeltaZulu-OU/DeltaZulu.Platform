using System.Globalization;
using System.Text;
using DeltaZulu.Platform.Domain.Analytics.Observability;

namespace DeltaZulu.Platform.Data.DuckDb.Ingestion;

public sealed class DuckDbAgentObservationWriter(SchemaApplier applier)
    : DuckDbAppendOnlyWriterBase<AgentObservationSnapshot>(applier)
{
    private const string TableName = "internal.AgentObservations";
    private const string ColumnList = "ObservedAt, TenantId, AgentId, HostId, Hostname, Platform, AgentVersion, LastSeenAt, IsEnabled, ReportedStatus, BufferPressure, QueueDepth, DroppedCount, ForwardFailedCount, DesiredConfigVersionId, AppliedConfigVersionId, DesiredProfileVersionId, AppliedProfileVersionId";

    protected override string BuildInsertSql(AgentObservationSnapshot s)
    {
        var sb = new StringBuilder(768);
        sb.Append($"INSERT INTO {TableName} ({ColumnList}) VALUES (");
        AppendRow(sb, s);
        sb.Append(");");
        return sb.ToString();
    }

    protected override string BuildBatchInsertSql(IReadOnlyList<AgentObservationSnapshot> snapshots)
    {
        var sb = new StringBuilder(256 + snapshots.Count * 384);
        sb.AppendLine($"INSERT INTO {TableName} ({ColumnList}) VALUES");

        for (var i = 0; i < snapshots.Count; i++)
        {
            if (i > 0) sb.AppendLine(",");
            sb.Append('(');
            AppendRow(sb, snapshots[i]);
            sb.Append(')');
        }

        sb.Append(';');
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, AgentObservationSnapshot s)
    {
        AppendTimestamp(sb, s.ObservedAtUtc);
        sb.Append(", ");
        AppendString(sb, s.TenantId);
        sb.Append(", ");
        AppendString(sb, s.AgentId);
        sb.Append(", ");
        AppendString(sb, s.HostId);
        sb.Append(", ");
        AppendString(sb, s.Hostname);
        sb.Append(", ");
        AppendString(sb, s.Platform);
        sb.Append(", ");
        AppendString(sb, s.AgentVersion);
        sb.Append(", ");
        AppendNullableTimestamp(sb, s.LastSeenAtUtc);
        sb.Append(", ");
        sb.Append(s.IsEnabled ? "true" : "false");
        sb.Append(", ");
        AppendString(sb, s.ReportedStatus);
        sb.Append(", ");
        sb.Append(s.BufferPressure.ToString(CultureInfo.InvariantCulture));
        sb.Append(", ");
        sb.Append(s.QueueDepth.ToString(CultureInfo.InvariantCulture));
        sb.Append(", ");
        sb.Append(s.DroppedCount.ToString(CultureInfo.InvariantCulture));
        sb.Append(", ");
        sb.Append(s.ForwardFailedCount.ToString(CultureInfo.InvariantCulture));
        sb.Append(", ");
        AppendNullableString(sb, s.DesiredConfigVersionId);
        sb.Append(", ");
        AppendNullableString(sb, s.AppliedConfigVersionId);
        sb.Append(", ");
        AppendNullableString(sb, s.DesiredProfileVersionId);
        sb.Append(", ");
        AppendNullableString(sb, s.AppliedProfileVersionId);
    }
}
