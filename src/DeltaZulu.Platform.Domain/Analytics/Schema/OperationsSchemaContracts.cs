namespace DeltaZulu.Platform.Domain.Analytics.Schema;

/// <summary>Read-only KQL contracts backed by append-only Operations lake evidence.</summary>
public static class OperationsSchemaContracts
{
    public static readonly InternalTableDef AlertEventsTable = new(
        "lake", "alert_events",
        [
            new("id", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("detection_id", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("detection_version", DuckDbType.Integer, KustoType.Int, Nullable: false),
            new("detection_run_id", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("alert_time_utc", DuckDbType.Timestamp, KustoType.DateTime, Nullable: false),
            new("source_view", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("source_event_id", DuckDbType.Varchar, KustoType.String),
            new("severity", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("confidence", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("risk_score", DuckDbType.Integer, KustoType.Int, Nullable: false),
            new("evidence_json", DuckDbType.Json, KustoType.Dynamic, Nullable: false),
            new("created_at_utc", DuckDbType.Timestamp, KustoType.DateTime, Nullable: false)
        ], "Append-only alert evidence lake table.");

    public static readonly InternalTableDef AlertEntitiesTable = new(
        "lake", "alert_entities",
        [
            new("id", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("alert_id", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("entity_type", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("entity_value", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("role", DuckDbType.Varchar, KustoType.String, Nullable: false),
            new("specificity_weight", DuckDbType.Double, KustoType.Real, Nullable: false),
            new("criticality_weight", DuckDbType.Double, KustoType.Real, Nullable: false),
            new("is_high_fanout", DuckDbType.Boolean, KustoType.Bool, Nullable: false),
            new("created_at_utc", DuckDbType.Timestamp, KustoType.DateTime, Nullable: false)
        ], "Append-only entities extracted from alert evidence.");

    public static readonly CanonicalViewDef AlertEvent = new(
        "golden", "AlertEvent", ["lake.alert_events"],
        [
            new("id", DuckDbType.Varchar, KustoType.String),
            new("detection_id", DuckDbType.Varchar, KustoType.String),
            new("detection_version", DuckDbType.Integer, KustoType.Int),
            new("detection_run_id", DuckDbType.Varchar, KustoType.String),
            new("alert_time_utc", DuckDbType.Timestamp, KustoType.DateTime),
            new("source_view", DuckDbType.Varchar, KustoType.String),
            new("source_event_id", DuckDbType.Varchar, KustoType.String),
            new("severity", DuckDbType.Varchar, KustoType.String),
            new("confidence", DuckDbType.Varchar, KustoType.String),
            new("risk_score", DuckDbType.Integer, KustoType.Int),
            new("evidence_json", DuckDbType.Json, KustoType.Dynamic),
            new("created_at_utc", DuckDbType.Timestamp, KustoType.DateTime)
        ], "Immutable alert evidence materialized by detection execution.");

    public static readonly CanonicalViewDef AlertEntity = new(
        "golden", "AlertEntity", ["lake.alert_entities"],
        [
            new("id", DuckDbType.Varchar, KustoType.String),
            new("alert_id", DuckDbType.Varchar, KustoType.String),
            new("entity_type", DuckDbType.Varchar, KustoType.String),
            new("entity_value", DuckDbType.Varchar, KustoType.String),
            new("role", DuckDbType.Varchar, KustoType.String),
            new("specificity_weight", DuckDbType.Double, KustoType.Real),
            new("criticality_weight", DuckDbType.Double, KustoType.Real),
            new("is_high_fanout", DuckDbType.Boolean, KustoType.Bool),
            new("created_at_utc", DuckDbType.Timestamp, KustoType.DateTime)
        ], "Immutable entity values extracted from alert evidence.");
}
