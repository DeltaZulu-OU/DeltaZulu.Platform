using Dapper;
using DeltaZulu.Platform.Domain.Analytics.Observability;

namespace DeltaZulu.Platform.Data.DuckDb.Analytics;

public sealed class DuckDbSourceMetricsReader : ISourceMetricsReader
{
    private readonly DuckDbConnectionFactory _connectionFactory;

    public DuckDbSourceMetricsReader(DuckDbConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public SourceHealthSummary ReadSummary()
    {
        var conn = _connectionFactory.GetConnection();
        var row = conn.QuerySingleOrDefault<SummaryRow>(
            "SELECT * FROM internal.v_source_health_summary");

        if (row is null)
        {
            return new SourceHealthSummary(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        return new SourceHealthSummary(
            row.source_count,
            row.agent_count,
            row.healthy_count,
            row.degraded_count,
            row.disabled_count,
            row.inactive_count,
            row.total_forwarded,
            row.total_discarded,
            row.total_forward_failed,
            row.total_read);
    }

    public IReadOnlyList<SourceLatestRow> ReadLatest(string? healthStatusFilter = null)
    {
        var conn = _connectionFactory.GetConnection();

        string sql;
        object? param = null;

        if (string.IsNullOrEmpty(healthStatusFilter))
        {
            sql = "SELECT * FROM internal.v_source_latest ORDER BY source_type, channel, agent_id";
        }
        else
        {
            sql = "SELECT * FROM internal.v_source_latest WHERE health_status = @Status ORDER BY source_type, channel, agent_id";
            param = new { Status = healthStatusFilter };
        }

        return conn.Query<LatestRow>(sql, param)
            .Select(static r => new SourceLatestRow(
                r.observed_at,
                r.agent_id,
                r.host_id,
                r.source_type,
                r.channel,
                r.is_enabled,
                r.can_read,
                r.read_error_count,
                r.read_count,
                r.kept_after_filter_count,
                r.discarded_count,
                r.forwarded_count,
                r.forward_failed_count,
                r.health_status,
                r.discard_ratio))
            .ToArray();
    }

    private sealed class SummaryRow
    {
        public long source_count { get; init; }
        public long agent_count { get; init; }
        public long healthy_count { get; init; }
        public long degraded_count { get; init; }
        public long disabled_count { get; init; }
        public long inactive_count { get; init; }
        public long total_forwarded { get; init; }
        public long total_discarded { get; init; }
        public long total_forward_failed { get; init; }
        public long total_read { get; init; }
    }

    private sealed class LatestRow
    {
        public DateTime observed_at { get; init; }
        public string agent_id { get; init; } = string.Empty;
        public string host_id { get; init; } = string.Empty;
        public string source_type { get; init; } = string.Empty;
        public string channel { get; init; } = string.Empty;
        public bool is_enabled { get; init; }
        public bool can_read { get; init; }
        public long read_error_count { get; init; }
        public long read_count { get; init; }
        public long kept_after_filter_count { get; init; }
        public long discarded_count { get; init; }
        public long forwarded_count { get; init; }
        public long forward_failed_count { get; init; }
        public string health_status { get; init; } = string.Empty;
        public double discard_ratio { get; init; }
    }
}
