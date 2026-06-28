using System.Globalization;
using System.Text;
using DeltaZulu.Platform.Domain.Analytics.Observability;

namespace DeltaZulu.Platform.Data.DuckDb.Ingestion;

public sealed class DuckDbSourceObservationWriter : IDisposable
{
    private const string TableName = "internal.source_observations";
    private readonly SchemaApplier _applier;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public DuckDbSourceObservationWriter(SchemaApplier applier)
    {
        ArgumentNullException.ThrowIfNull(applier);
        _applier = applier;
    }

    public async Task AppendAsync(SourceObservationSnapshot snapshot, CancellationToken cancellationToken = default)
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

    public async Task AppendBatchAsync(IReadOnlyList<SourceObservationSnapshot> snapshots, CancellationToken cancellationToken = default)
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

    private static string BuildInsertSql(SourceObservationSnapshot s)
    {
        var sb = new StringBuilder(512);
        sb.Append($"INSERT INTO {TableName} (observed_at, window_start, window_end, agent_id, host_id, source_type, channel, is_enabled, can_read, read_error_count, read_count, kept_after_filter_count, discarded_count, forwarded_count, forward_failed_count) VALUES (");
        AppendRow(sb, s);
        sb.Append(");");
        return sb.ToString();
    }

    private static string BuildBatchInsertSql(IReadOnlyList<SourceObservationSnapshot> snapshots)
    {
        var sb = new StringBuilder(256 + snapshots.Count * 256);
        sb.AppendLine($"INSERT INTO {TableName} (observed_at, window_start, window_end, agent_id, host_id, source_type, channel, is_enabled, can_read, read_error_count, read_count, kept_after_filter_count, discarded_count, forwarded_count, forward_failed_count) VALUES");

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
        sb.Append("TIMESTAMP '");
        sb.Append(FormatTimestamp(s.ObservedAtUtc));
        sb.Append("', ");
        sb.Append("NULL, NULL, ");
        sb.Append('\'');
        sb.Append(EscapeSql(s.AgentId));
        sb.Append("', '");
        sb.Append(EscapeSql(s.HostId));
        sb.Append("', '");
        sb.Append(EscapeSql(s.SourceType));
        sb.Append("', '");
        sb.Append(EscapeSql(s.Channel));
        sb.Append("', ");
        sb.Append(s.IsEnabled ? "true" : "false");
        sb.Append(", ");
        sb.Append(s.CanRead ? "true" : "false");
        sb.Append(", ");
        sb.Append(s.ReadErrorCount.ToString(CultureInfo.InvariantCulture));
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

    private static string FormatTimestamp(DateTime utc) =>
        utc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static string EscapeSql(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}
