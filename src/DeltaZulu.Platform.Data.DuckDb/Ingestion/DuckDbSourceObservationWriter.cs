using System.Text;
using Dapper;
using DeltaZulu.Platform.Domain.Analytics.Observability;

namespace DeltaZulu.Platform.Data.DuckDb.Ingestion;

public sealed class DuckDbSourceObservationWriter(SchemaApplier applier)
    : DuckDbAppendOnlyWriterBase<SourceObservationSnapshot>(applier)
{
    private const string TableName = "internal.SourceObservations";
    private const string ColumnList = "ObservedAt, WindowStart, WindowEnd, TenantId, AgentId, HostId, SourceInstanceId, SourceType, ResourceFamily, Provider, Channel, ProfileId, ProfileVersionId, IsEnabled, CanRead, LastReadAt, ReadErrorCount, LastError, ReadCount, KeptAfterFilterCount, DiscardedCount, ForwardedCount, ForwardFailedCount";

    protected override (string Sql, DynamicParameters Parameters) BuildInsert(SourceObservationSnapshot s)
    {
        var sb = new StringBuilder(768);
        var parameters = new DynamicParameters();
        var index = 0;

        sb.Append($"INSERT INTO {TableName} ({ColumnList}) VALUES ");
        AppendRowPlaceholders(sb, parameters, ref index, RowValues(s));
        sb.Append(';');

        return (sb.ToString(), parameters);
    }

    protected override (string Sql, DynamicParameters Parameters) BuildBatchInsert(IReadOnlyList<SourceObservationSnapshot> snapshots)
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

    private static object?[] RowValues(SourceObservationSnapshot s) =>
    [
        s.ObservedAtUtc,
        s.WindowStartUtc,
        s.WindowEndUtc,
        s.TenantId,
        s.AgentId,
        s.HostId,
        NullIfEmpty(s.SourceInstanceId),
        s.SourceType,
        NullIfEmpty(s.ResourceFamily),
        NullIfEmpty(s.Provider),
        s.Channel,
        NullIfEmpty(s.ProfileId),
        NullIfEmpty(s.ProfileVersionId),
        s.IsEnabled,
        s.CanRead,
        s.LastReadAtUtc,
        s.ReadErrorCount,
        NullIfEmpty(s.LastError),
        s.ReadCount,
        s.KeptAfterFilterCount,
        s.DiscardedCount,
        s.ForwardedCount,
        s.ForwardFailedCount,
    ];
}
