namespace Hunting.Core.Schema.Definitions;

using Mapping;
using static Mapping.MapDsl;

/// <summary>
/// Canonical schema for DeviceNetworkEvents — Microsoft Defender Advanced Hunting compatible.
/// Parser: Sysmon Event ID 3 (Network Connection detected).
///
/// This is the second table family in the POC. Adding it proves that the schema model,
/// DDL emitter, parser view construction, and catalog generalize beyond one table.
///
/// Sysmon EID 3 JSON field mapping:
///   SourceIp              → LocalIP
///   SourcePort            → LocalPort
///   DestinationIp         → RemoteIP
///   DestinationPort       → RemotePort
///   DestinationHostname   → RemoteUrl
///   Protocol              → Protocol
///   Image                 → InitiatingProcessFileName (basename) + FolderPath
///   ProcessId             → InitiatingProcessId
///   User                  → InitiatingProcessAccountName
/// </summary>
public static class DeviceNetworkEventsSchema
{
    public static readonly IReadOnlyList<ColumnDef> Columns =
    [
        new("Timestamp",                        DuckDbType.Timestamp, KustoType.DateTime),
        new("DeviceName",                       DuckDbType.Varchar,   KustoType.String),
        new("ActionType",                       DuckDbType.Varchar,   KustoType.String),
        new("LocalIP",                          DuckDbType.Varchar,   KustoType.String),
        new("LocalPort",                        DuckDbType.Integer,   KustoType.Int),
        new("RemoteIP",                         DuckDbType.Varchar,   KustoType.String),
        new("RemotePort",                       DuckDbType.Integer,   KustoType.Int),
        new("Protocol",                         DuckDbType.Varchar,   KustoType.String),
        new("RemoteUrl",                        DuckDbType.Varchar,   KustoType.String),
        new("LocalIPType",                      DuckDbType.Varchar,   KustoType.String),
        new("RemoteIPType",                     DuckDbType.Varchar,   KustoType.String),
        new("InitiatingProcessFileName",        DuckDbType.Varchar,   KustoType.String),
        new("InitiatingProcessFolderPath",      DuckDbType.Varchar,   KustoType.String),
        new("InitiatingProcessId",              DuckDbType.BigInt,    KustoType.Long),
        new("InitiatingProcessCommandLine",     DuckDbType.Varchar,   KustoType.String),
        new("InitiatingProcessAccountName",     DuckDbType.Varchar,   KustoType.String),
        new("InitiatingProcessSHA256",          DuckDbType.Varchar,   KustoType.String),
        new("ReportId",                         DuckDbType.Varchar,   KustoType.String),
    ];

    public static readonly CanonicalViewDef View = new(
        Schema:      "main",
        Name:        "DeviceNetworkEvents",
        ParserViews: ["internal.v_network_sysmon_connect"],
        Columns:     Columns,
        Description: "Network connection events from all configured sources");

    public static readonly ParserViewDef SysmonNetworkConnect = new(
        Schema:          "internal",
        Name:            "v_network_sysmon_connect",
        SourceName:      "Microsoft Sysmon Event ID 3",
        CanonicalTarget: "DeviceNetworkEvents",
        Mapping: new MappingQueryDef(
            SourceObject: "raw.windows_event_json",
            Filter: And(
                Eq(Col("provider"), Lit("Microsoft-Windows-Sysmon")),
                Eq(Col("event_id"), Lit(3))),
            Projections:
            [
                Map("Timestamp",                        Col("ingest_time")),
                Map("DeviceName",                       Col("computer")),
                Map("ActionType",                       Lit("ConnectionSuccess")),
                Map("LocalIP",                          JsonText(Col("event_data"), "$.SourceIp")),
                Map("LocalPort",                        Cast(JsonText(Col("event_data"), "$.SourcePort"),      DuckDbType.Integer)),
                Map("RemoteIP",                         JsonText(Col("event_data"), "$.DestinationIp")),
                Map("RemotePort",                       Cast(JsonText(Col("event_data"), "$.DestinationPort"), DuckDbType.Integer)),
                Map("Protocol",                         JsonText(Col("event_data"), "$.Protocol")),
                Map("RemoteUrl",                        JsonText(Col("event_data"), "$.DestinationHostname")),
                Map("LocalIPType",                      Lit(null)),
                Map("RemoteIPType",                     Lit(null)),
                Map("InitiatingProcessFileName",        RegexExtract(JsonText(Col("event_data"), "$.Image"), @"[^\\]+$", 0)),
                Map("InitiatingProcessFolderPath",      JsonText(Col("event_data"), "$.Image")),
                Map("InitiatingProcessId",              Cast(JsonText(Col("event_data"), "$.ProcessId"), DuckDbType.BigInt)),
                Map("InitiatingProcessCommandLine",     Lit(null)),
                Map("InitiatingProcessAccountName",     JsonText(Col("event_data"), "$.User")),
                Map("InitiatingProcessSHA256",          JsonText(Col("event_data"), "$.Hashes")),
                Map("ReportId",                         Lit(null)),
            ]),
        Columns:     Columns,
        Description: "Sysmon Event ID 3 (Network Connection) parser");
}
