namespace DeltaZulu.Platform.Domain.Hunting.Schema.Definitions.Internal;

using DeltaZulu.Platform.Domain.Hunting.Schema;

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

    public static IReadOnlyList<InternalTableDef> InternalTables =>
    [
        SchemaProvenance,
        SeedBatches
    ];
}