namespace DeltaZulu.Platform.Domain.Analytics.Schema.Definitions.Medallion.Golden;

/// <summary>
/// Canonical KQL view contracts for alert lake records.
/// These map directly to DuckDB lake tables (no medallion parser views).
/// </summary>
public static class AlertContracts
{
    public static readonly IReadOnlyList<ColumnDef> AlertEventColumns =
    [
        new("Id", DuckDbType.Varchar, KustoType.String, Description: "Unique alert identifier."),
        new("DetectionId", DuckDbType.Varchar, KustoType.String, Description: "Detection rule identifier that produced the alert."),
        new("DetectionVersion", DuckDbType.Integer, KustoType.Int, Description: "Accepted detection content version at alert time."),
        new("DetectionRunId", DuckDbType.Varchar, KustoType.String, Description: "Detection run that produced the alert."),
        new("AlertTimeUtc", DuckDbType.Timestamp, KustoType.DateTime, Description: "Time the alert was raised (UTC)."),
        new("SourceView", DuckDbType.Varchar, KustoType.String, Description: "Canonical view that matched the detection rule."),
        new("SourceEventId", DuckDbType.Varchar, KustoType.String, Description: "Source event identifier when available."),
        new("Severity", DuckDbType.Varchar, KustoType.String, Description: "Alert severity."),
        new("Confidence", DuckDbType.Varchar, KustoType.String, Description: "Detection confidence."),
        new("RiskScore", DuckDbType.Integer, KustoType.Int, Description: "Computed risk score (0–100)."),
        new("EvidenceJson", DuckDbType.Json, KustoType.Dynamic, Description: "Raw evidence payload from the matching event."),
        new("CreatedAtUtc", DuckDbType.Timestamp, KustoType.DateTime, Description: "Timestamp when the alert was written to the lake (UTC).")
    ];

    public static readonly CanonicalViewDef AlertEvent = new(
        Schema: "golden",
        Name: "AlertEvent",
        ParserViews: [],
        Columns: AlertEventColumns,
        Description: "Immutable alert records written to the DuckDB lake by the mediation daemon.");

    public static readonly IReadOnlyList<ColumnDef> AlertEntityColumns =
    [
        new("Id", DuckDbType.Varchar, KustoType.String, Description: "Unique alert-entity identifier."),
        new("AlertId", DuckDbType.Varchar, KustoType.String, Description: "Parent alert identifier."),
        new("EntityType", DuckDbType.Varchar, KustoType.String, Description: "Entity type (e.g. Host, User, IP, Domain)."),
        new("EntityValue", DuckDbType.Varchar, KustoType.String, Description: "Canonical entity value."),
        new("Role", DuckDbType.Varchar, KustoType.String, Description: "Entity role in the alert (e.g. Subject, Target)."),
        new("SpecificityWeight", DuckDbType.Double, KustoType.Real, Description: "How specific the entity is (1.0 = highly specific)."),
        new("CriticalityWeight", DuckDbType.Double, KustoType.Real, Description: "Criticality contribution weight for candidate scoring."),
        new("IsHighFanout", DuckDbType.Boolean, KustoType.Bool, Description: "True when the entity appears in too many alerts to be discriminating."),
        new("CreatedAtUtc", DuckDbType.Timestamp, KustoType.DateTime, Description: "Timestamp when the entity was extracted (UTC).")
    ];

    public static readonly CanonicalViewDef AlertEntity = new(
        Schema: "golden",
        Name: "AlertEntity",
        ParserViews: [],
        Columns: AlertEntityColumns,
        Description: "Extracted entity records associated with lake alert events.");
}
