using System.Globalization;
using System.Text;
using DeltaZulu.Platform.Domain.Analytics.Observability;

namespace DeltaZulu.Platform.Data.DuckDb.Ingestion;

public sealed class DuckDbSourceObservationWriter(SchemaApplier applier)
    : DuckDbAppendOnlyWriterBase<SourceObservationSnapshot>(applier)
{
    private const string TableName = "internal.SourceObservations";
    private const string ColumnList = "ObservedAt, WindowStart, WindowEnd, TenantId, AgentId, HostId, SourceInstanceId, SourceType, ResourceFamily, Provider, Channel, ProfileId, ProfileVersionId, IsEnabled, CanRead, LastReadAt, ReadErrorCount, LastError, ReadCount, KeptAfterFilterCount, DiscardedCount, ForwardedCount, ForwardFailedCount";

    protected override string BuildInsertSql(SourceObservationSnapshot s)
    {
        var sb = new StringBuilder(768);
        sb.Append($"INSERT INTO {TableName} ({ColumnList}) VALUES (");
        AppendRow(sb, s);
        sb.Append(");");
        return sb.ToString();
    }

    protected override string BuildBatchInsertSql(IReadOnlyList<SourceObservationSnapshot> snapshots)
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

    private static void AppendRow(StringBuilder sb, SourceObservationSnapshot s)
    {
        AppendTimestamp(sb, s.ObservedAtUtc);
        sb.Append(", ");
        AppendNullableTimestamp(sb, s.WindowStartUtc);
        sb.Append(", ");
        AppendNullableTimestamp(sb, s.WindowEndUtc);
        sb.Append(", ");
        AppendString(sb, s.TenantId);
        sb.Append(", ");
        AppendString(sb, s.AgentId);
        sb.Append(", ");
        AppendString(sb, s.HostId);
        sb.Append(", ");
        AppendNullableString(sb, s.SourceInstanceId);
        sb.Append(", ");
        AppendString(sb, s.SourceType);
        sb.Append(", ");
        AppendNullableString(sb, s.ResourceFamily);
        sb.Append(", ");
        AppendNullableString(sb, s.Provider);
        sb.Append(", ");
        AppendString(sb, s.Channel);
        sb.Append(", ");
        AppendNullableString(sb, s.ProfileId);
        sb.Append(", ");
        AppendNullableString(sb, s.ProfileVersionId);
        sb.Append(", ");
        sb.Append(s.IsEnabled ? "true" : "false");
        sb.Append(", ");
        sb.Append(s.CanRead ? "true" : "false");
        sb.Append(", ");
        AppendNullableTimestamp(sb, s.LastReadAtUtc);
        sb.Append(", ");
        sb.Append(s.ReadErrorCount.ToString(CultureInfo.InvariantCulture));
        sb.Append(", ");
        AppendNullableString(sb, s.LastError);
        sb.Append(", ");
        sb.Append(s.ReadCount.ToString(CultureInfo.InvariantCulture));
        sb.Append(", ");
        sb.Append(s.KeptAfterFilterCount.ToString(CultureInfo.InvariantCulture));
        sb.Append(", ");
        sb.Append(s.DiscardedCount.ToString(CultureInfo.InvariantCulture));
        sb.Append(", ");
        sb.Append(s.ForwardedCount.ToString(CultureInfo.InvariantCulture));
        sb.Append(", ");
        sb.Append(s.ForwardFailedCount.ToString(CultureInfo.InvariantCulture));
    }
}
