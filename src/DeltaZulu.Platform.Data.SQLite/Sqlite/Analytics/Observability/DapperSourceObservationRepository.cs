using Dapper;
using DeltaZulu.Platform.Domain.Analytics.Observability;
using static DeltaZulu.Platform.Data.Sqlite.Analytics.SqliteDateTimeHelpers;

namespace DeltaZulu.Platform.Data.Sqlite.Analytics.Observability;

public sealed class DapperSourceObservationRepository : DapperRepositoryBase, ISourceObservationRepository
{
    private const string CreateSchemaSql =
        """
        CREATE TABLE IF NOT EXISTS source_observations (
            source_type              TEXT NOT NULL,
            channel                  TEXT NOT NULL,
            agent_id                 TEXT NOT NULL,
            host_id                  TEXT NOT NULL,
            is_enabled               INTEGER NOT NULL DEFAULT 1,
            can_read                 INTEGER NOT NULL DEFAULT 0,
            last_read_at_utc         TEXT,
            read_error_count         INTEGER NOT NULL DEFAULT 0,
            last_error               TEXT,
            read_count               INTEGER NOT NULL DEFAULT 0,
            kept_after_filter_count  INTEGER NOT NULL DEFAULT 0,
            discarded_count          INTEGER NOT NULL DEFAULT 0,
            forwarded_count          INTEGER NOT NULL DEFAULT 0,
            forward_failed_count     INTEGER NOT NULL DEFAULT 0,
            observed_at_utc          TEXT NOT NULL,
            PRIMARY KEY (source_type, channel, agent_id)
        );

        CREATE INDEX IF NOT EXISTS idx_source_observations_health
            ON source_observations (is_enabled, can_read, observed_at_utc DESC);
        """;

    private const string SelectColumns =
        """
        source_type             AS SourceType,
        channel                 AS Channel,
        agent_id                AS AgentId,
        host_id                 AS HostId,
        is_enabled              AS IsEnabled,
        can_read                AS CanRead,
        last_read_at_utc        AS LastReadAtUtc,
        read_error_count        AS ReadErrorCount,
        last_error              AS LastError,
        read_count              AS ReadCount,
        kept_after_filter_count AS KeptAfterFilterCount,
        discarded_count         AS DiscardedCount,
        forwarded_count         AS ForwardedCount,
        forward_failed_count    AS ForwardFailedCount,
        observed_at_utc         AS ObservedAtUtc
        """;

    private const string ListLatestSql =
        $"SELECT {SelectColumns} FROM source_observations ORDER BY source_type, channel, agent_id;";

    private const string UpsertSql =
        """
        INSERT INTO source_observations (
            source_type, channel, agent_id, host_id,
            is_enabled, can_read, last_read_at_utc, read_error_count, last_error,
            read_count, kept_after_filter_count, discarded_count,
            forwarded_count, forward_failed_count, observed_at_utc
        ) VALUES (
            @SourceType, @Channel, @AgentId, @HostId,
            @IsEnabled, @CanRead, @LastReadAtUtc, @ReadErrorCount, @LastError,
            @ReadCount, @KeptAfterFilterCount, @DiscardedCount,
            @ForwardedCount, @ForwardFailedCount, @ObservedAtUtc
        )
        ON CONFLICT(source_type, channel, agent_id) DO UPDATE SET
            host_id                 = excluded.host_id,
            is_enabled              = excluded.is_enabled,
            can_read                = excluded.can_read,
            last_read_at_utc        = excluded.last_read_at_utc,
            read_error_count        = excluded.read_error_count,
            last_error              = excluded.last_error,
            read_count              = excluded.read_count,
            kept_after_filter_count = excluded.kept_after_filter_count,
            discarded_count         = excluded.discarded_count,
            forwarded_count         = excluded.forwarded_count,
            forward_failed_count    = excluded.forward_failed_count,
            observed_at_utc         = excluded.observed_at_utc;
        """;

    public DapperSourceObservationRepository(IAppDbConnectionFactory connectionFactory)
        : base(connectionFactory, CreateSchemaSql)
    {
    }

    public async Task<IReadOnlyList<SourceObservationSnapshot>> ListLatestAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<SourceObservationRow>(
            new CommandDefinition(ListLatestSql, cancellationToken: cancellationToken));
        return rows.Select(ToSnapshot).ToArray();
    }

    public async Task UpsertAsync(SourceObservationSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(UpsertSql, new {
            snapshot.SourceType,
            snapshot.Channel,
            snapshot.AgentId,
            snapshot.HostId,
            IsEnabled           = snapshot.IsEnabled ? 1 : 0,
            CanRead             = snapshot.CanRead ? 1 : 0,
            LastReadAtUtc       = FormatNullable(snapshot.LastReadAtUtc),
            snapshot.ReadErrorCount,
            snapshot.LastError,
            snapshot.ReadCount,
            snapshot.KeptAfterFilterCount,
            snapshot.DiscardedCount,
            snapshot.ForwardedCount,
            snapshot.ForwardFailedCount,
            ObservedAtUtc       = Format(snapshot.ObservedAtUtc),
        }, cancellationToken: cancellationToken));
    }

    private static SourceObservationSnapshot ToSnapshot(SourceObservationRow r) => new(
        r.SourceType,
        r.Channel,
        r.AgentId,
        r.HostId,
        r.IsEnabled != 0,
        r.CanRead != 0,
        ParseNullable(r.LastReadAtUtc),
        r.ReadErrorCount,
        r.LastError,
        r.ReadCount,
        r.KeptAfterFilterCount,
        r.DiscardedCount,
        r.ForwardedCount,
        r.ForwardFailedCount,
        Parse(r.ObservedAtUtc));

    private sealed class SourceObservationRow
    {
        public string SourceType { get; init; } = string.Empty;
        public string Channel { get; init; } = string.Empty;
        public string AgentId { get; init; } = string.Empty;
        public string HostId { get; init; } = string.Empty;
        public int IsEnabled { get; init; }
        public int CanRead { get; init; }
        public string? LastReadAtUtc { get; init; }
        public long ReadErrorCount { get; init; }
        public string? LastError { get; init; }
        public long ReadCount { get; init; }
        public long KeptAfterFilterCount { get; init; }
        public long DiscardedCount { get; init; }
        public long ForwardedCount { get; init; }
        public long ForwardFailedCount { get; init; }
        public string ObservedAtUtc { get; init; } = string.Empty;
    }
}
