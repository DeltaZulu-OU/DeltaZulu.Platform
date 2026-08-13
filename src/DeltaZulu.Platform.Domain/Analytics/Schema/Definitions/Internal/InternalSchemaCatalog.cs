namespace DeltaZulu.Platform.Domain.Analytics.Schema.Definitions.Internal;

/// <summary>
/// Internal metadata schema objects used by the hunting database runtime.
/// These tables are not exposed to KQL users and are not part of the Golden surface.
/// </summary>
public static class InternalSchemaCatalog
{
    public const string InternalSchema = "internal";
    public const string SchemaProvenanceTableName = "SchemaProvenance";
    public const string SeedBatchesTableName = "SeedBatches";

    /// <summary>Zero-safe ratio: 0 when the denominator is not positive, else numerator/denominator as a double.</summary>
    private static string SafeRatio(string numerator, string denominator) =>
        $"CASE WHEN {denominator} > 0 THEN CAST({numerator} AS DOUBLE) / ({denominator}) ELSE 0 END";

    public static readonly InternalTableDef SchemaProvenance = new(
        InternalSchema,
        SchemaProvenanceTableName,
        [
            new("ObjectName", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Fully qualified schema object name, for example golden.ProcessEvent."),
            new("ObjectKind", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Schema object kind, such as table, parser_view, or canonical_view."),
            new("SchemaHash", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Stable fingerprint of the emitted schema object definition."),
            new("CatalogVersion", DuckDbType.Varchar, KustoType.String, Nullable: true,
                Description: "Optional catalog or application schema version that produced the object."),
            new("AppliedAt", DuckDbType.Timestamp, KustoType.DateTime, Nullable: false,
                Description: "Timestamp when this provenance row was written.")
        ],
        Description: "Internal ledger of applied schema object fingerprints.");

    public static readonly InternalTableDef SeedBatches = new(
        InternalSchema,
        SeedBatchesTableName,
        [
            new("BatchId", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Stable identifier for the seed fixture batch."),
            new("TableName", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Fully qualified target table for the seed batch."),
            new("SourceName", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Source family or source system represented by the batch."),
            new("Scenario", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Scenario label, for example process.persistence or dns.nxdomain."),
            new("RowCount", DuckDbType.BigInt, KustoType.Long, Nullable: false,
                Description: "Expected number of rows inserted by the seed batch."),
            new("ContentHash", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Stable SHA-256 hash of the seed batch SQL/content."),
            new("CatalogVersion", DuckDbType.Varchar, KustoType.String, Nullable: true,
                Description: "Optional seed/catalog version that produced the batch."),
            new("AppliedAt", DuckDbType.Timestamp, KustoType.DateTime, Nullable: false,
                Description: "Timestamp when this seed batch was recorded.")
        ],
        Description: "Internal ledger of applied governed seed fixture batches.");

    public static readonly InternalTableDef SourceObservations = new(
        InternalSchema,
        "SourceObservations",
        [
            new("ObservedAt", DuckDbType.Timestamp, KustoType.DateTime, Nullable: false,
                Description: "Timestamp when this source observation was recorded by the agent."),
            new("WindowStart", DuckDbType.Timestamp, KustoType.DateTime, Nullable: true,
                Description: "Start of the observation measurement window."),
            new("WindowEnd", DuckDbType.Timestamp, KustoType.DateTime, Nullable: true,
                Description: "End of the observation measurement window."),
            new("TenantId", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Tenant identity. Required on every lake metric fact for safe joins."),
            new("AgentId", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Reporting agent identity."),
            new("HostId", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Host or device where the agent runs."),
            new("SourceInstanceId", DuckDbType.Varchar, KustoType.String, Nullable: true,
                Description: "Stable configured source instance identity when available."),
            new("SourceType", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Log source type such as WindowsEventLog, ETW, Sysmon, or DNSServer."),
            new("ResourceFamily", DuckDbType.Varchar, KustoType.String, Nullable: true,
                Description: "Normalized resource family such as EventLog, ETW, File, Process, or DNS."),
            new("Provider", DuckDbType.Varchar, KustoType.String, Nullable: true,
                Description: "Provider or publisher name when applicable."),
            new("Channel", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Source channel such as Security or Sysmon/Operational."),
            new("ProfileId", DuckDbType.Varchar, KustoType.String, Nullable: true,
                Description: "Control-plane profile identity projected into the lake."),
            new("ProfileVersionId", DuckDbType.Varchar, KustoType.String, Nullable: true,
                Description: "Control-plane profile version projected into the lake."),
            new("IsEnabled", DuckDbType.Boolean, KustoType.Bool, Nullable: false,
                Description: "Whether the source was enabled at observation time."),
            new("CanRead", DuckDbType.Boolean, KustoType.Bool, Nullable: false,
                Description: "Whether the source was readable at observation time."),
            new("LastReadAt", DuckDbType.Timestamp, KustoType.DateTime, Nullable: true,
                Description: "Last successful source read reported by the agent."),
            new("ReadErrorCount", DuckDbType.BigInt, KustoType.Long, Nullable: false,
                Description: "Read errors observed in the window."),
            new("LastError", DuckDbType.Varchar, KustoType.String, Nullable: true,
                Description: "Latest source read or forwarding error reported by the agent."),
            new("ReadCount", DuckDbType.BigInt, KustoType.Long, Nullable: false,
                Description: "Events read from the source in the window."),
            new("KeptAfterFilterCount", DuckDbType.BigInt, KustoType.Long, Nullable: false,
                Description: "Events retained after filter evaluation."),
            new("DiscardedCount", DuckDbType.BigInt, KustoType.Long, Nullable: false,
                Description: "Events discarded by filter rules."),
            new("ForwardedCount", DuckDbType.BigInt, KustoType.Long, Nullable: false,
                Description: "Events successfully forwarded to the platform."),
            new("ForwardFailedCount", DuckDbType.BigInt, KustoType.Long, Nullable: false,
                Description: "Events that failed to forward.")
        ],
        Description: "Append-only time-series ledger of agent source observation snapshots.");

    public static readonly InternalTableDef AgentObservations = new(
        InternalSchema,
        "AgentObservations",
        [
            new("ObservedAt", DuckDbType.Timestamp, KustoType.DateTime, Nullable: false,
                Description: "Timestamp when this agent observation was recorded."),
            new("TenantId", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Tenant identity. Required on every lake metric fact for safe joins."),
            new("AgentId", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Agent identity."),
            new("HostId", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Stable host identity reported by the agent."),
            new("Hostname", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Human-readable host name reported by the agent."),
            new("Platform", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Agent platform such as Windows, Linux, macOS, or appliance."),
            new("AgentVersion", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Runtime agent version."),
            new("LastSeenAt", DuckDbType.Timestamp, KustoType.DateTime, Nullable: true,
                Description: "Last heartbeat or control-plane check-in time."),
            new("IsEnabled", DuckDbType.Boolean, KustoType.Bool, Nullable: false,
                Description: "Whether the agent is enabled in control-plane state."),
            new("ReportedStatus", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Raw status reported by the agent."),
            new("BufferPressure", DuckDbType.Double, KustoType.Real, Nullable: false,
                Description: "Agent buffer pressure as a 0..1 value."),
            new("QueueDepth", DuckDbType.BigInt, KustoType.Long, Nullable: false,
                Description: "Queued events or chunks waiting to flush."),
            new("DroppedCount", DuckDbType.BigInt, KustoType.Long, Nullable: false,
                Description: "Events or chunks dropped by local policy."),
            new("ForwardFailedCount", DuckDbType.BigInt, KustoType.Long, Nullable: false,
                Description: "Forwarding failures reported by the agent."),
            new("DesiredConfigVersionId", DuckDbType.Varchar, KustoType.String, Nullable: true,
                Description: "Desired daemon config version from the control plane."),
            new("AppliedConfigVersionId", DuckDbType.Varchar, KustoType.String, Nullable: true,
                Description: "Daemon config version currently applied by the agent."),
            new("DesiredProfileVersionId", DuckDbType.Varchar, KustoType.String, Nullable: true,
                Description: "Desired source profile version from the control plane."),
            new("AppliedProfileVersionId", DuckDbType.Varchar, KustoType.String, Nullable: true,
                Description: "Source profile version currently applied by the agent.")
        ],
        Description: "Append-only time-series ledger of agent runtime and projected control-plane state.");

    public static readonly InternalViewDef SourceLatest = new(
        InternalSchema,
        "SourceLatest",
        SqlBody: $"""
            SELECT
                ObservedAt,
                TenantId,
                AgentId,
                HostId,
                SourceIdentity,
                SourceInstanceId,
                SourceType,
                ResourceFamily,
                Provider,
                Channel,
                ProfileId,
                ProfileVersionId,
                IsEnabled,
                CanRead,
                LastReadAt,
                ReadErrorCount,
                LastError,
                ReadCount,
                KeptAfterFilterCount,
                DiscardedCount,
                ForwardedCount,
                ForwardFailedCount,
                CASE
                    WHEN NOT IsEnabled THEN 'Disabled'
                    WHEN NOT CanRead OR ReadErrorCount > 0 OR ForwardFailedCount > 0 THEN 'Degraded'
                    WHEN ReadCount = 0 THEN 'Inactive'
                    ELSE 'Healthy'
                END AS HealthStatus,
                {SafeRatio("DiscardedCount", "ReadCount")} AS DiscardRatio
            FROM (
                SELECT *,
                    COALESCE(NULLIF(SourceInstanceId, ''), SourceType || ':' || Channel) AS SourceIdentity,
                    ROW_NUMBER() OVER (
                        PARTITION BY TenantId, AgentId, COALESCE(NULLIF(SourceInstanceId, ''), SourceType || ':' || Channel)
                        ORDER BY ObservedAt DESC
                    ) AS _rn
                FROM internal.SourceObservations
            ) sub
            WHERE _rn = 1
            """,
        [
            new("ObservedAt", DuckDbType.Timestamp, KustoType.DateTime, Nullable: false),
            new("TenantId", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("AgentId", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("HostId", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("SourceIdentity", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("SourceInstanceId", DuckDbType.Varchar, KustoType.String, Nullable: true),
            new("SourceType", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("ResourceFamily", DuckDbType.Varchar, KustoType.String, Nullable: true),
            new("Provider", DuckDbType.Varchar, KustoType.String, Nullable: true),
            new("Channel", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("ProfileId", DuckDbType.Varchar, KustoType.String, Nullable: true),
            new("ProfileVersionId", DuckDbType.Varchar, KustoType.String, Nullable: true),
            new("IsEnabled", DuckDbType.Boolean, KustoType.Bool, Nullable: false),
            new("CanRead", DuckDbType.Boolean, KustoType.Bool, Nullable: false),
            new("LastReadAt", DuckDbType.Timestamp, KustoType.DateTime, Nullable: true),
            new("ReadErrorCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("LastError", DuckDbType.Varchar, KustoType.String, Nullable: true),
            new("ReadCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("KeptAfterFilterCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("DiscardedCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("ForwardedCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("ForwardFailedCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("HealthStatus", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("DiscardRatio", DuckDbType.Double, KustoType.Real, Nullable: false)
        ],
        Description: "Latest observation per tenant/source/agent with computed health status.");

    public static readonly InternalViewDef AgentLatest = new(
        InternalSchema,
        "AgentLatest",
        SqlBody: """
            SELECT
                ObservedAt,
                TenantId,
                AgentId,
                HostId,
                Hostname,
                Platform,
                AgentVersion,
                LastSeenAt,
                IsEnabled,
                ReportedStatus,
                BufferPressure,
                QueueDepth,
                DroppedCount,
                ForwardFailedCount,
                DesiredConfigVersionId,
                AppliedConfigVersionId,
                DesiredProfileVersionId,
                AppliedProfileVersionId,
                CASE
                    WHEN DesiredConfigVersionId IS NULL AND AppliedConfigVersionId IS NULL
                         AND DesiredProfileVersionId IS NULL AND AppliedProfileVersionId IS NULL THEN 'Unknown'
                    WHEN DesiredConfigVersionId IS NULL OR DesiredProfileVersionId IS NULL THEN 'UnknownDesired'
                    WHEN AppliedConfigVersionId IS NULL OR AppliedProfileVersionId IS NULL THEN 'UnknownApplied'
                    WHEN DesiredConfigVersionId IS DISTINCT FROM AppliedConfigVersionId
                         OR DesiredProfileVersionId IS DISTINCT FROM AppliedProfileVersionId THEN 'Drifted'
                    ELSE 'InSync'
                END AS ConfigDriftStatus,
                (DesiredConfigVersionId IS NOT NULL AND AppliedConfigVersionId IS NOT NULL
                    AND DesiredProfileVersionId IS NOT NULL AND AppliedProfileVersionId IS NOT NULL
                    AND (DesiredConfigVersionId IS DISTINCT FROM AppliedConfigVersionId
                        OR DesiredProfileVersionId IS DISTINCT FROM AppliedProfileVersionId)) AS ConfigDrift,
                CASE
                    WHEN NOT IsEnabled THEN 'Disabled'
                    WHEN LastSeenAt IS NULL THEN 'Offline'
                    WHEN current_timestamp - LastSeenAt > INTERVAL '15 minutes' THEN 'Stale'
                    ELSE 'Online'
                END AS ConnectivityStatus,
                CASE
                    WHEN BufferPressure >= 0.85 OR DroppedCount > 0 OR ForwardFailedCount > 0 THEN 'Degraded'
                    ELSE 'Nominal'
                END AS PipelineStatus,
                CASE
                    WHEN NOT IsEnabled THEN 'Disabled'
                    WHEN LastSeenAt IS NULL THEN 'Offline'
                    WHEN current_timestamp - LastSeenAt > INTERVAL '15 minutes' THEN 'Stale'
                    WHEN BufferPressure >= 0.85 OR DroppedCount > 0 OR ForwardFailedCount > 0 THEN 'Degraded'
                    ELSE 'Online'
                END AS HealthStatus
            FROM (
                SELECT *,
                    ROW_NUMBER() OVER (
                        PARTITION BY TenantId, AgentId
                        ORDER BY ObservedAt DESC
                    ) AS _rn
                FROM internal.AgentObservations
            ) sub
            WHERE _rn = 1
            """,
        [
            new("ObservedAt", DuckDbType.Timestamp, KustoType.DateTime, Nullable: false),
            new("TenantId", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("AgentId", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("HostId", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("Hostname", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("Platform", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("AgentVersion", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("LastSeenAt", DuckDbType.Timestamp, KustoType.DateTime, Nullable: true),
            new("IsEnabled", DuckDbType.Boolean, KustoType.Bool, Nullable: false),
            new("ReportedStatus", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("BufferPressure", DuckDbType.Double, KustoType.Real, Nullable: false),
            new("QueueDepth", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("DroppedCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("ForwardFailedCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("DesiredConfigVersionId", DuckDbType.Varchar, KustoType.String, Nullable: true),
            new("AppliedConfigVersionId", DuckDbType.Varchar, KustoType.String, Nullable: true),
            new("DesiredProfileVersionId", DuckDbType.Varchar, KustoType.String, Nullable: true),
            new("AppliedProfileVersionId", DuckDbType.Varchar, KustoType.String, Nullable: true),
            new("ConfigDriftStatus", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("ConfigDrift", DuckDbType.Boolean, KustoType.Bool, Nullable: false),
            new("ConnectivityStatus", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("PipelineStatus", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("HealthStatus", DuckDbType.Varchar, KustoType.String, Nullable: false)
        ],
        Description: "Latest observation per tenant/agent with computed health status and drift.");

    public static readonly InternalViewDef SourceHealthSummary = new(
        InternalSchema,
        "SourceHealthSummary",
        SqlBody: $"""
            SELECT
                TenantId,
                COUNT(*) AS SourceCount,
                COUNT(DISTINCT AgentId) AS AgentCount,
                COUNT(*) FILTER (WHERE HealthStatus = 'Healthy') AS HealthyCount,
                COUNT(*) FILTER (WHERE HealthStatus = 'Degraded') AS DegradedCount,
                COUNT(*) FILTER (WHERE HealthStatus = 'Disabled') AS DisabledCount,
                COUNT(*) FILTER (WHERE HealthStatus = 'Inactive') AS InactiveCount,
                COALESCE(SUM(ForwardedCount), 0) AS TotalForwarded,
                COALESCE(SUM(DiscardedCount), 0) AS TotalDiscarded,
                COALESCE(SUM(ForwardFailedCount), 0) AS TotalForwardFailed,
                COALESCE(SUM(ReadCount), 0) AS TotalRead,
                COALESCE(SUM(KeptAfterFilterCount), 0) AS TotalKept,
                COALESCE(SUM(ReadErrorCount), 0) AS TotalReadErrors,
                {SafeRatio("COALESCE(SUM(DiscardedCount), 0)", "COALESCE(SUM(ReadCount), 0)")} AS OverallDiscardRatio,
                {SafeRatio("COALESCE(SUM(ForwardedCount), 0)", "COALESCE(SUM(ReadCount), 0)")} AS ForwardingYield,
                {SafeRatio("COALESCE(SUM(ForwardFailedCount), 0)", "COALESCE(SUM(ForwardedCount), 0) + COALESCE(SUM(ForwardFailedCount), 0)")} AS ForwardFailureRate,
                {SafeRatio("COALESCE(SUM(ReadErrorCount), 0)", "COALESCE(SUM(ReadCount), 0)")} AS ReadErrorRate
            FROM internal.SourceLatest
            GROUP BY TenantId
            """,
        [
            new("TenantId", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("SourceCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("AgentCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("HealthyCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("DegradedCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("DisabledCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("InactiveCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalForwarded", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalDiscarded", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalForwardFailed", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalRead", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalKept", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalReadErrors", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("OverallDiscardRatio", DuckDbType.Double, KustoType.Real, Nullable: false),
            new("ForwardingYield", DuckDbType.Double, KustoType.Real, Nullable: false),
            new("ForwardFailureRate", DuckDbType.Double, KustoType.Real, Nullable: false),
            new("ReadErrorRate", DuckDbType.Double, KustoType.Real, Nullable: false)
        ],
        Description: "Tenant-scoped aggregate health and utilization summary across latest source observations.");

    public static readonly InternalViewDef AgentHealthSummary = new(
        InternalSchema,
        "AgentHealthSummary",
        SqlBody: """
            SELECT
                TenantId,
                COUNT(*) AS AgentCount,
                COUNT(*) FILTER (WHERE HealthStatus = 'Online') AS OnlineCount,
                COUNT(*) FILTER (WHERE HealthStatus = 'Stale') AS StaleCount,
                COUNT(*) FILTER (WHERE HealthStatus = 'Offline') AS OfflineCount,
                COUNT(*) FILTER (WHERE HealthStatus = 'Degraded') AS DegradedCount,
                COUNT(*) FILTER (WHERE HealthStatus = 'Disabled') AS DisabledCount,
                COUNT(*) FILTER (WHERE ConfigDrift) AS ConfigDriftCount,
                COALESCE(SUM(QueueDepth), 0) AS TotalQueueDepth,
                COALESCE(SUM(DroppedCount), 0) AS TotalDropped,
                COALESCE(SUM(ForwardFailedCount), 0) AS TotalForwardFailed,
                COALESCE(MAX(BufferPressure), 0) AS MaxBufferPressure
            FROM internal.AgentLatest
            GROUP BY TenantId
            """,
        [
            new("TenantId", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("AgentCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("OnlineCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("StaleCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("OfflineCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("DegradedCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("DisabledCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("ConfigDriftCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalQueueDepth", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalDropped", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalForwardFailed", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("MaxBufferPressure", DuckDbType.Double, KustoType.Real, Nullable: false)
        ],
        Description: "Tenant-scoped aggregate health summary across latest agent observations.");

    public static readonly InternalViewDef ObservedCollectionCoverage = new(
        InternalSchema,
        "ObservedCollectionCoverage",
        SqlBody: """
            SELECT
                a.TenantId,
                a.AgentId,
                a.Hostname,
                a.Platform,
                a.HealthStatus AS AgentHealthStatus,
                COUNT(s.AgentId) AS SourceCount,
                COUNT(*) FILTER (WHERE s.HealthStatus = 'Healthy') AS HealthySourceCount,
                COUNT(*) FILTER (WHERE s.HealthStatus = 'Degraded') AS DegradedSourceCount,
                COUNT(*) FILTER (WHERE s.HealthStatus = 'Inactive') AS InactiveSourceCount,
                COUNT(*) FILTER (WHERE s.HealthStatus = 'Disabled') AS DisabledSourceCount,
                COALESCE(SUM(s.ForwardedCount), 0) AS TotalForwarded,
                COALESCE(MAX(a.DroppedCount), 0) AS LatestAgentDroppedCount,
                COALESCE(SUM(s.ForwardFailedCount), 0) AS SourceForwardFailedCount,
                COALESCE(MAX(a.ForwardFailedCount), 0) AS AgentForwardFailedCount
            FROM internal.AgentLatest a
            LEFT JOIN internal.SourceLatest s
                ON s.TenantId = a.TenantId
               AND s.AgentId = a.AgentId
            GROUP BY a.TenantId, a.AgentId, a.Hostname, a.Platform, a.HealthStatus
            """,
        [
            new("TenantId", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("AgentId", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("Hostname", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("Platform", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("AgentHealthStatus", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("SourceCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("HealthySourceCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("DegradedSourceCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("InactiveSourceCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("DisabledSourceCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalForwarded", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("LatestAgentDroppedCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("SourceForwardFailedCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("AgentForwardFailedCount", DuckDbType.BigInt, KustoType.Long, Nullable: false)
        ],
        Description: "Observed-only agent-to-source coverage surface.");

    public static readonly InternalViewDef OverviewSummary = new(
        InternalSchema,
        "OverviewSummary",
        SqlBody: """
            SELECT
                COALESCE(a.TenantId, s.TenantId) AS TenantId,
                COALESCE(a.AgentCount, 0) AS AgentCount,
                COALESCE(a.OnlineCount, 0) AS OnlineAgentCount,
                COALESCE(a.DegradedCount, 0) AS DegradedAgentCount,
                COALESCE(a.StaleCount, 0) AS StaleAgentCount,
                COALESCE(a.OfflineCount, 0) AS OfflineAgentCount,
                COALESCE(a.ConfigDriftCount, 0) AS ConfigDriftCount,
                COALESCE(s.SourceCount, 0) AS SourceCount,
                COALESCE(s.HealthyCount, 0) AS HealthySourceCount,
                COALESCE(s.DegradedCount, 0) AS DegradedSourceCount,
                COALESCE(s.InactiveCount, 0) AS InactiveSourceCount,
                COALESCE(s.DisabledCount, 0) AS DisabledSourceCount,
                COALESCE(s.TotalRead, 0) AS TotalRead,
                COALESCE(s.TotalKept, 0) AS TotalKept,
                COALESCE(s.TotalDiscarded, 0) AS TotalDiscarded,
                COALESCE(s.TotalForwarded, 0) AS TotalForwarded,
                COALESCE(s.TotalForwardFailed, 0) AS SourceForwardFailedCount,
                COALESCE(a.TotalForwardFailed, 0) AS AgentForwardFailedCount,
                COALESCE(s.OverallDiscardRatio, 0) AS OverallDiscardRatio,
                COALESCE(a.MaxBufferPressure, 0) AS MaxBufferPressure
            FROM internal.AgentHealthSummary a
            FULL OUTER JOIN internal.SourceHealthSummary s
                ON s.TenantId = a.TenantId
            """,
        [
            new("TenantId", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("AgentCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("OnlineAgentCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("DegradedAgentCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("StaleAgentCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("OfflineAgentCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("ConfigDriftCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("SourceCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("HealthySourceCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("DegradedSourceCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("InactiveSourceCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("DisabledSourceCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalRead", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalKept", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalDiscarded", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalForwarded", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("SourceForwardFailedCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("AgentForwardFailedCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("OverallDiscardRatio", DuckDbType.Double, KustoType.Real, Nullable: false),
            new("MaxBufferPressure", DuckDbType.Double, KustoType.Real, Nullable: false)
        ],
        Description: "Tenant-scoped high-level Overview dashboard metric surface.");

    public static readonly InternalViewDef SourceUtilization = new(
        InternalSchema,
        "SourceUtilization",
        SqlBody: $"""
            SELECT
                *,
                {SafeRatio("ForwardedCount", "ReadCount")} AS ForwardingYield,
                {SafeRatio("ForwardFailedCount", "ForwardedCount + ForwardFailedCount")} AS ForwardFailureRate,
                {SafeRatio("ReadErrorCount", "ReadCount")} AS ReadErrorRate
            FROM internal.SourceLatest
            """,
        [
            new("ForwardingYield", DuckDbType.Double, KustoType.Real, Nullable: false,
                Description: "Fraction of read events that were successfully forwarded (ForwardedCount / ReadCount)."),
            new("ForwardFailureRate", DuckDbType.Double, KustoType.Real, Nullable: false,
                Description: "Fraction of forward attempts that failed (ForwardFailedCount / (ForwardedCount + ForwardFailedCount))."),
            new("ReadErrorRate", DuckDbType.Double, KustoType.Real, Nullable: false,
                Description: "Read errors per event read (ReadErrorCount / ReadCount).")
        ],
        Description: "Per-source utilization: SourceLatest plus forwarding yield, forward failure rate, and read error rate.");

    public static readonly InternalViewDef SourceUtilizationByProfile = new(
        InternalSchema,
        "SourceUtilizationByProfile",
        SqlBody: $"""
            SELECT
                TenantId,
                COALESCE(NULLIF(ProfileId, ''), '(unassigned)') AS ProfileId,
                COUNT(*) AS SourceCount,
                COUNT(DISTINCT AgentId) AS AgentCount,
                COALESCE(SUM(ReadCount), 0) AS TotalRead,
                COALESCE(SUM(KeptAfterFilterCount), 0) AS TotalKept,
                COALESCE(SUM(DiscardedCount), 0) AS TotalDiscarded,
                COALESCE(SUM(ForwardedCount), 0) AS TotalForwarded,
                COALESCE(SUM(ForwardFailedCount), 0) AS TotalForwardFailed,
                COALESCE(SUM(ReadErrorCount), 0) AS TotalReadErrors,
                {SafeRatio("COALESCE(SUM(ForwardedCount), 0)", "COALESCE(SUM(ReadCount), 0)")} AS ForwardingYield,
                {SafeRatio("COALESCE(SUM(DiscardedCount), 0)", "COALESCE(SUM(ReadCount), 0)")} AS DiscardRatio,
                {SafeRatio("COALESCE(SUM(ForwardFailedCount), 0)", "COALESCE(SUM(ForwardedCount), 0) + COALESCE(SUM(ForwardFailedCount), 0)")} AS ForwardFailureRate,
                {SafeRatio("COALESCE(SUM(ReadErrorCount), 0)", "COALESCE(SUM(ReadCount), 0)")} AS ReadErrorRate
            FROM internal.SourceLatest
            GROUP BY TenantId, COALESCE(NULLIF(ProfileId, ''), '(unassigned)')
            """,
        [
            new("TenantId", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("ProfileId", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Resource profile identifier, or '(unassigned)' for sources with no profile linkage."),
            new("SourceCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("AgentCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalRead", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalKept", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalDiscarded", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalForwarded", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalForwardFailed", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalReadErrors", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("ForwardingYield", DuckDbType.Double, KustoType.Real, Nullable: false),
            new("DiscardRatio", DuckDbType.Double, KustoType.Real, Nullable: false),
            new("ForwardFailureRate", DuckDbType.Double, KustoType.Real, Nullable: false),
            new("ReadErrorRate", DuckDbType.Double, KustoType.Real, Nullable: false)
        ],
        Description: "Utilization rolled up by resource profile across every agent that uses it — the collection-quality view scoped to what an operator can actually edit.");

    public static readonly InternalViewDef AgentUtilization = new(
        InternalSchema,
        "AgentUtilization",
        SqlBody: $"""
            SELECT
                a.TenantId,
                a.AgentId,
                a.Hostname,
                a.HealthStatus AS AgentHealthStatus,
                a.DroppedCount,
                a.BufferPressure,
                COALESCE(SUM(s.ReadCount), 0) AS TotalRead,
                COALESCE(SUM(s.ForwardedCount), 0) AS TotalForwarded,
                COALESCE(SUM(s.DiscardedCount), 0) AS TotalDiscarded,
                COALESCE(SUM(s.ForwardFailedCount), 0) AS TotalForwardFailed,
                {SafeRatio("a.DroppedCount", "a.DroppedCount + COALESCE(SUM(s.ForwardedCount), 0)")} AS BufferDropRatio
            FROM internal.AgentLatest a
            LEFT JOIN internal.SourceLatest s
                ON s.TenantId = a.TenantId
               AND s.AgentId = a.AgentId
            GROUP BY a.TenantId, a.AgentId, a.Hostname, a.HealthStatus, a.DroppedCount, a.BufferPressure
            """,
        [
            new("TenantId", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("AgentId", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("Hostname", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("AgentHealthStatus", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("DroppedCount", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("BufferPressure", DuckDbType.Double, KustoType.Real, Nullable: false),
            new("TotalRead", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalForwarded", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalDiscarded", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("TotalForwardFailed", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("BufferDropRatio", DuckDbType.Double, KustoType.Real, Nullable: false,
                Description: "Fraction of buffer-eligible volume lost to buffer-overflow drops rather than forwarded (DroppedCount / (DroppedCount + TotalForwarded)). Distinguishes true data loss from intentional filter discard.")
        ],
        Description: "Agent-level utilization: read/forward/discard totals across its sources plus the buffer drop ratio.");

    public static IReadOnlyList<InternalTableDef> InternalTables =>
    [
        SchemaProvenance,
        SeedBatches,
        SourceObservations,
        AgentObservations
    ];

    public static IReadOnlyList<InternalViewDef> InternalViews =>
    [
        SourceLatest,
        AgentLatest,
        SourceHealthSummary,
        AgentHealthSummary,
        ObservedCollectionCoverage,
        OverviewSummary,
        SourceUtilization,
        SourceUtilizationByProfile,
        AgentUtilization
    ];
}
