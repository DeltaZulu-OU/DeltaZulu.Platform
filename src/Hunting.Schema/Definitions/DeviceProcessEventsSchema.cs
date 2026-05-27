namespace Hunting.Schema.Definitions;

using Hunting.Core.Mapping;
using Hunting.Core.Schema;
using static Hunting.Core.Mapping.MapDsl;

/// <summary>
/// Canonical column definitions and parser view mappings for DeviceProcessEvents.
/// This is the first vertical slice — one public hunting view backed by one parser view.
/// </summary>
public static class DeviceProcessEventsSchema
{
    public static readonly IReadOnlyList<ColumnDef> Columns =
    [
        new("Timestamp",                    DuckDbType.Timestamp, KustoType.DateTime, Description: "Event time"),
        new("DeviceId",                     DuckDbType.Varchar,   KustoType.String,   Description: "Device identifier"),
        new("DeviceName",                   DuckDbType.Varchar,   KustoType.String,   Description: "Host name"),
        new("ActionType",                   DuckDbType.Varchar,   KustoType.String,   Description: "Process action category"),
        new("FileName",                     DuckDbType.Varchar,   KustoType.String,   Description: "Process image file name"),
        new("FolderPath",                   DuckDbType.Varchar,   KustoType.String,   Description: "Process image path"),
        new("SHA256",                       DuckDbType.Varchar,   KustoType.String,   Description: "File hash"),
        new("ProcessId",                    DuckDbType.BigInt,    KustoType.Long,     Description: "Process ID"),
        new("ProcessCommandLine",           DuckDbType.Varchar,   KustoType.String,   Description: "Command line"),
        new("AccountName",                  DuckDbType.Varchar,   KustoType.String,   Description: "User name"),
        new("InitiatingProcessFileName",    DuckDbType.Varchar,   KustoType.String,   Description: "Parent process file name"),
        new("InitiatingProcessCommandLine", DuckDbType.Varchar,   KustoType.String,   Description: "Parent command line"),
        new("ReportId",                     DuckDbType.Varchar,   KustoType.String,   Description: "Source report identifier"),
        new("AdditionalFields",             DuckDbType.Json,      KustoType.Dynamic,  Description: "Source-specific additional data"),
    ];

    /// <summary>
    /// The public hunting view exposed to KQL users.
    /// </summary>
    public static readonly CanonicalViewDef View = new(
        Schema: "golden",
        Name: "ProcessEvents",
        ParserViews: ["silver.v_process_sysmon_create"],
        Columns: Columns,
        Description: "Process creation and related events across sources");

    // ─── Raw table ──────────────────────────────────────────────────────

    public static readonly RawTableDef RawWindowsEventJson = new(
        Schema: "bronze",
        Name: "windows_event_json",
        Columns:
        [
            new("ingest_time",  DuckDbType.Timestamp, KustoType.DateTime),
            new("source_type",  DuckDbType.Varchar,   KustoType.String),
            new("provider",     DuckDbType.Varchar,   KustoType.String),
            new("event_id",     DuckDbType.Integer,   KustoType.Int),
            new("computer",     DuckDbType.Varchar,   KustoType.String),
            new("event_data",   DuckDbType.Json,      KustoType.Dynamic),
            new("raw_text",     DuckDbType.Varchar,   KustoType.String),
        ],
        SourceDescription: "Windows Event Log JSON records");

    // ─── Sysmon process-create parser view ───────────────────────────────

    public static readonly ParserViewDef SysmonProcessCreate = new(
        Schema: "silver",
        Name: "v_process_sysmon_create",
        SourceName: "Microsoft Sysmon Event ID 1",
        CanonicalTarget: "ProcessEvents",
        Mapping: new MappingQueryDef(
            SourceObject: "bronze.windows_event_json",
            Filter: And(
                Eq(Col("provider"), Lit("Microsoft-Windows-Sysmon")),
                Eq(Col("event_id"), Lit(1))),
            Projections:
            [
                Map("Timestamp",                    Col("ingest_time")),
                Map("DeviceId",                     Lit(null)),
                Map("DeviceName",                   Col("computer")),
                Map("ActionType",                   Lit("ProcessCreated")),
                Map("FileName",                     RegexExtract(JsonText(Col("event_data"), "$.Image"), @"[^\\]+$", 0)),
                Map("FolderPath",                   JsonText(Col("event_data"), "$.Image")),
                Map("SHA256",                       JsonText(Col("event_data"), "$.Hashes")),
                Map("ProcessId",                    Cast(JsonText(Col("event_data"), "$.ProcessId"), DuckDbType.BigInt)),
                Map("ProcessCommandLine",           JsonText(Col("event_data"), "$.CommandLine")),
                Map("AccountName",                  JsonText(Col("event_data"), "$.User")),
                Map("InitiatingProcessFileName",    RegexExtract(JsonText(Col("event_data"), "$.ParentImage"), @"[^\\]+$", 0)),
                Map("InitiatingProcessCommandLine", JsonText(Col("event_data"), "$.ParentCommandLine")),
                Map("ReportId",                     Lit(null)),
                Map("AdditionalFields",             Col("event_data")),
            ]),
        Columns: Columns,
        Description: "Sysmon Event ID 1 (Process Create) parser");
}
