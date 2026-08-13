namespace DeltaZulu.Platform.Domain.Analytics.Schema.Definitions.Internal;

/// <summary>
/// Internal metadata schema objects used by the hunting database runtime.
/// These tables are not exposed to KQL users and are not part of the Golden surface.
/// </summary>
public static class InternalSchemaCatalog
{
    public const string InternalSchema = "internal";
    public const string SchemaProvenanceTableName = "schema_provenance";
    public const string SeedBatchesTableName = "seed_batches";

    public static readonly InternalTableDef SchemaProvenance = new(
        InternalSchema,
        SchemaProvenanceTableName,
        [
            new("object_name", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Fully qualified schema object name, for example golden.ProcessEvent."),
            new("object_kind", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Schema object kind, such as table, parser_view, or canonical_view."),
            new("schema_hash", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Stable fingerprint of the emitted schema object definition."),
            new("catalog_version", DuckDbType.Varchar, KustoType.String, Nullable: true,
                Description: "Optional catalog or application schema version that produced the object."),
            new("applied_at", DuckDbType.Timestamp, KustoType.DateTime, Nullable: false,
                Description: "Timestamp when this provenance row was written.")
        ],
        Description: "Internal ledger of applied schema object fingerprints.");

    public static readonly InternalTableDef SeedBatches = new(
        InternalSchema,
        SeedBatchesTableName,
        [
            new("batch_id", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Stable identifier for the seed fixture batch."),
            new("table_name", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Fully qualified target table for the seed batch."),
            new("source_name", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Source family or source system represented by the batch."),
            new("scenario", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Scenario label, for example process.persistence or dns.nxdomain."),
            new("row_count", DuckDbType.BigInt, KustoType.Long, Nullable: false,
                Description: "Expected number of rows inserted by this batch."),
            new("content_hash", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Stable SHA-256 hash of the seed batch SQL/content."),
            new("catalog_version", DuckDbType.Varchar, KustoType.String, Nullable: true,
                Description: "Optional seed/catalog version that produced the batch."),
            new("applied_at", DuckDbType.Timestamp, KustoType.DateTime, Nullable: false,
                Description: "Timestamp when this seed batch was recorded.")
        ],
        Description: "Internal ledger of applied governed seed fixture batches.");

    public static readonly InternalTableDef SourceObservations = new(
        InternalSchema,
        "source_observations",
        [
            new("observed_at", DuckDbType.Timestamp, KustoType.DateTime, Nullable: false,
                Description: "Timestamp when this observation was recorded by the agent."),
            new("window_start", DuckDbType.Timestamp, KustoType.DateTime, Nullable: true,
                Description: "Start of the observation measurement window."),
            new("window_end", DuckDbType.Timestamp, KustoType.DateTime, Nullable: true,
                Description: "End of the observation measurement window."),
            new("agent_id", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Reporting agent identity."),
            new("host_id", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Host or device where the agent runs."),
            new("source_type", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Log source type such as WindowsEventLog or DNSServer."),
            new("channel", DuckDbType.Varchar, KustoType.String, Nullable: false,
                Description: "Source channel such as Security or Sysmon/Operational."),
            new("is_enabled", DuckDbType.Boolean, KustoType.Bool, Nullable: false,
                Description: "Whether the source was enabled at observation time."),
            new("can_read", DuckDbType.Boolean, KustoType.Bool, Nullable: false,
                Description: "Whether the source was readable at observation time."),
            new("read_error_count", DuckDbType.BigInt, KustoType.Long, Nullable: false,
                Description: "Cumulative read errors observed in the window."),
            new("read_count", DuckDbType.BigInt, KustoType.Long, Nullable: false,
                Description: "Events read from the source in the window."),
            new("kept_after_filter_count", DuckDbType.BigInt, KustoType.Long, Nullable: false,
                Description: "Events retained after filter evaluation."),
            new("discarded_count", DuckDbType.BigInt, KustoType.Long, Nullable: false,
                Description: "Events discarded by filter rules."),
            new("forwarded_count", DuckDbType.BigInt, KustoType.Long, Nullable: false,
                Description: "Events successfully forwarded to the platform."),
            new("forward_failed_count", DuckDbType.BigInt, KustoType.Long, Nullable: false,
                Description: "Events that failed to forward.")
        ],
        Description: "Append-only time-series ledger of agent source observation snapshots.");

    public static readonly InternalViewDef SourceLatest = new(
        InternalSchema,
        "v_source_latest",
        SqlBody: """
            SELECT
                observed_at,
                agent_id,
                host_id,
                source_type,
                channel,
                is_enabled,
                can_read,
                read_error_count,
                read_count,
                kept_after_filter_count,
                discarded_count,
                forwarded_count,
                forward_failed_count,
                CASE
                    WHEN NOT is_enabled THEN 'Disabled'
                    WHEN NOT can_read OR read_error_count > 0 OR forward_failed_count > 0 THEN 'Degraded'
                    WHEN read_count = 0 THEN 'Inactive'
                    ELSE 'Healthy'
                END AS health_status,
                CASE WHEN read_count > 0
                    THEN CAST(discarded_count AS DOUBLE) / read_count
                    ELSE 0
                END AS discard_ratio
            FROM (
                SELECT *,
                    ROW_NUMBER() OVER (
                        PARTITION BY source_type, channel, agent_id
                        ORDER BY observed_at DESC
                    ) AS _rn
                FROM internal.source_observations
            ) sub
            WHERE _rn = 1
            """,
        [
            new("observed_at", DuckDbType.Timestamp, KustoType.DateTime, Nullable: false),
            new("agent_id", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("host_id", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("source_type", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("channel", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("is_enabled", DuckDbType.Boolean, KustoType.Bool, Nullable: false),
            new("can_read", DuckDbType.Boolean, KustoType.Bool, Nullable: false),
            new("read_error_count", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("read_count", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("kept_after_filter_count", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("discarded_count", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("forwarded_count", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("forward_failed_count", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("health_status", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("discard_ratio", DuckDbType.Double, KustoType.Real, Nullable: false)
        ],
        Description: "Latest observation per source/agent with computed health status.");

    public static readonly InternalViewDef SourceHealthSummary = new(
        InternalSchema,
        "v_source_health_summary",
        SqlBody: """
            SELECT
                COUNT(DISTINCT (source_type, channel)) AS source_count,
                COUNT(DISTINCT agent_id) AS agent_count,
                COUNT(*) FILTER (WHERE health_status = 'Healthy') AS healthy_count,
                COUNT(*) FILTER (WHERE health_status = 'Degraded') AS degraded_count,
                COUNT(*) FILTER (WHERE health_status = 'Disabled') AS disabled_count,
                COUNT(*) FILTER (WHERE health_status = 'Inactive') AS inactive_count,
                COALESCE(SUM(forwarded_count), 0) AS total_forwarded,
                COALESCE(SUM(discarded_count), 0) AS total_discarded,
                COALESCE(SUM(forward_failed_count), 0) AS total_forward_failed,
                COALESCE(SUM(read_count), 0) AS total_read
            FROM internal.v_source_latest
            """,
        [
            new("source_count", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("agent_count", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("healthy_count", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("degraded_count", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("disabled_count", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("inactive_count", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("total_forwarded", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("total_discarded", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("total_forward_failed", DuckDbType.BigInt, KustoType.Long, Nullable: false),
            new("total_read", DuckDbType.BigInt, KustoType.Long, Nullable: false)
        ],
        Description: "Single-row aggregate health summary across all latest source observations.");

    public static IReadOnlyList<InternalTableDef> InternalTables =>
    [
        SchemaProvenance,
        SeedBatches,
        SourceObservations
    ];

    public static IReadOnlyList<InternalViewDef> InternalViews =>
    [
        SourceLatest,
        SourceHealthSummary
    ];
}