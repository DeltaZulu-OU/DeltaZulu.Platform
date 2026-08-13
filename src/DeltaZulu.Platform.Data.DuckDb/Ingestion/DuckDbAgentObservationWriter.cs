using System.Globalization;
using System.Text;
using DeltaZulu.Platform.Domain.Analytics.Observability;

namespace DeltaZulu.Platform.Data.DuckDb.Ingestion;

public sealed class DuckDbAgentObservationWriter : IDisposable
{
    private const string TableName = "internal.AgentObservations";
    private const string ColumnList = "ObservedAt, TenantId, AgentId, HostId, Hostname, Platform, AgentVersion, LastSeenAt, IsEnabled, ReportedStatus, BufferPressure, QueueDepth, DroppedCount, ForwardFailedCount, DesiredConfigVersionId, AppliedConfigVersionId, DesiredProfileVersionId, AppliedProfileVersionId";

    private readonly SchemaApplier _applier;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public DuckDbAgentObservationWriter(SchemaApplier applier)
    {
        ArgumentNullException.ThrowIfNull(applier);
        _applier = applier;
    }

    public async Task AppendAsync(AgentObservationSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            _applier.ExecuteRaw(BuildInsertSql(snapshot));
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task AppendBatchAsync(IReadOnlyList<AgentObservationSnapshot> snapshots, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        if (snapshots.Count == 0) return;

        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            _applier.ExecuteRaw(BuildBatchInsertSql(snapshots));
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public void Dispose() => _writeGate.Dispose();

    private static string BuildInsertSql(AgentObservationSnapshot s)
    {
        var sb = new StringBuilder(768);
        sb.Append($"INSERT INTO {TableName} ({ColumnList}) VALUES (");
        AppendRow(sb, s);
        sb.Append(");");
        return sb.ToString();
    }

    private static string BuildBatchInsertSql(IReadOnlyList<AgentObservationSnapshot> snapshots)
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

    private static void AppendTimestamp(StringBuilder sb, DateTime utc)
    {
        sb.Append("TIMESTAMP '");
        sb.Append(FormatTimestamp(utc));
        sb.Append('\'');
    }

    private static void AppendNullableTimestamp(StringBuilder sb, DateTime? utc)
    {
        if (utc is null)
        {
            sb.Append("NULL");
            return;
        }

        AppendTimestamp(sb, utc.Value);
    }

    private static void AppendString(StringBuilder sb, string value)
    {
        sb.Append('\'');
        sb.Append(EscapeSql(value));
        sb.Append('\'');
    }

    private static void AppendNullableString(StringBuilder sb, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            sb.Append("NULL");
            return;
        }

        AppendString(sb, value);
    }

    private static string FormatTimestamp(DateTime utc) =>
        utc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static string EscapeSql(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}
