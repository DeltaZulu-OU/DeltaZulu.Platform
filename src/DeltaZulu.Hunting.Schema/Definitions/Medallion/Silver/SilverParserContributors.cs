namespace DeltaZulu.Hunting.Schema.Definitions.Medallion.Silver;

using DeltaZulu.Hunting.Core.Mapping;
using DeltaZulu.Hunting.Core.Schema;
using DeltaZulu.Hunting.Schema.Definitions.Medallion.Golden;
using static Hunting.Core.Mapping.MapDsl;

/// <summary>
/// <para>Active Phase 1A Silver parser contributors.</para>
/// <para>
/// All active contributors keep source-specific interpretation in Silver:
/// selectors, event-time extraction, tolerant optional conversions, and Golden
/// projections are declared per source/event shape.
/// </para>
/// </summary>
public static class SilverParserContributors
{
    public static readonly ParserViewDef DnsServerQueryEvent = CreateParser(
     schema: "silver",
     name: "v_dns_server_query_event",
     sourceName: "Windows DNS Server EVTX query event",
     canonicalTarget: "Dns",
     sourceObject: "bronze.dns_server_event",
     filter: WindowsDnsServerEvtxQuerySelector(),
     columns: GoldenEventContracts.DnsColumns,
     projections:
     [
         Map("Timestamp", Fn(
            "COALESCE",
            TryCast(JsonText(Col("raw_log"), "$.TimeCreated"), DuckDbType.Timestamp),
            TryCast(JsonText(Col("raw_log"), "$.EventTime"), DuckDbType.Timestamp),
            Col("ingest_time"))),

        Map("DeviceName", Col("host")),

        Map("ActionType", Lit("DnsQuery")),

        Map("QueryName", Fn(
            "COALESCE",
            JsonText(Col("raw_log"), "$.QueryName"),
            JsonText(Col("raw_log"), "$.QNAME"))),

        Map("QueryType", Fn(
            "COALESCE",
            JsonText(Col("raw_log"), "$.QueryType"),
            JsonText(Col("raw_log"), "$.QTYPE"))),

        Map("ResponseCode", Fn(
            "COALESCE",
            JsonText(Col("raw_log"), "$.ResponseCode"),
            JsonText(Col("raw_log"), "$.RCODE"))),

        Map("ResponseName", Fn(
            "COALESCE",
            JsonText(Col("raw_log"), "$.ResponseName"),
            JsonText(Col("raw_log"), "$.AnswerName"))),

        Map("ResponseIP", Fn(
            "COALESCE",
            JsonText(Col("raw_log"), "$.ResponseIP"),
            JsonText(Col("raw_log"), "$.ResponseIp"),
            JsonText(Col("raw_log"), "$.AnswerIP"))),

        Map("SrcIpAddr", Fn(
            "COALESCE",
            JsonText(Col("raw_log"), "$.ClientIp"),
            JsonText(Col("raw_log"), "$.ClientIP"),
            JsonText(Col("raw_log"), "$.SourceIp"),
            JsonText(Col("raw_log"), "$.SourceIP"))),

        Map("SrcPortNumber", TryCast(Fn(
            "COALESCE",
            JsonText(Col("raw_log"), "$.ClientPort"),
            JsonText(Col("raw_log"), "$.SourcePort")), DuckDbType.Integer)),

        Map("Protocol", JsonText(Col("raw_log"), "$.Protocol")),

        Map("ReportId", Fn(
            "COALESCE",
            JsonText(Col("raw_log"), "$.EventRecordID"),
            JsonText(Col("raw_log"), "$.RecordId"),
            JsonText(Col("raw_log"), "$.EventID"))),

        Map("AdditionalFields", Col("raw_log"))
     ],
     description: "Windows DNS Server EVTX-style query contributor for Dns.");

    public static readonly ParserViewDef DnsWindowsSysmonEid22 = CreateParser(
        schema: "silver",
        name: "v_dns_windows_sysmon_eid22",
        sourceName: "Windows Sysmon Event ID 22",
        canonicalTarget: "Dns",
        sourceObject: "bronze.windows_sysmon_event",
        filter: EventIdEquals("22"),
        columns: GoldenEventContracts.DnsColumns,
        projections:
        [
            Map("Timestamp", EventTimestamp("$.UtcTime")),
            Map("DeviceName", Col("host")),
            Map("ActionType", Lit("DnsQuery")),
            Map("QueryName", JsonText(Col("raw_log"), "$.QueryName")),
            Map("QueryType", Lit(null)),
            Map("ResponseCode", JsonText(Col("raw_log"), "$.QueryStatus")),
            Map("ResponseName", JsonText(Col("raw_log"), "$.QueryResults")),
            Map("ResponseIP", Lit(null)),
            Map("SrcIpAddr", Lit(null)),
            Map("SrcPortNumber", Lit(null)),
            Map("Protocol", Lit(null)),
            Map("ReportId", JsonText(Col("raw_log"), "$.EventRecordID")),
            Map("AdditionalFields", Col("raw_log"))
        ],
        description: "Sysmon Event ID 22 contributor for Dns.");

    public static readonly ParserViewDef NetworkSessionWindowsSecurityEid5156 = CreateParser(
        schema: "silver",
        name: "v_networksession_windows_security_eid5156",
        sourceName: "Windows Security Event ID 5156",
        canonicalTarget: "NetworkSession",
        sourceObject: "bronze.windows_security_event",
        filter: EventIdEquals("5156"),
        columns: GoldenEventContracts.NetworkSessionColumns,
        projections:
        [
            Map("Timestamp", EventTimestamp("$.TimeCreated")),
            Map("DeviceName", Col("host")),
            Map("ActionType", Lit("ConnectionAllowed")),
            Map("LocalIP", JsonText(Col("raw_log"), "$.SourceAddress")),
            Map("LocalPort", TryCast(JsonText(Col("raw_log"), "$.SourcePort"), DuckDbType.Integer)),
            Map("RemoteIP", JsonText(Col("raw_log"), "$.DestAddress")),
            Map("RemotePort", TryCast(JsonText(Col("raw_log"), "$.DestPort"), DuckDbType.Integer)),
            Map("Protocol", JsonText(Col("raw_log"), "$.Protocol")),
            Map("RemoteUrl", Lit(null)),
            Map("LocalIPType", Lit(null)),
            Map("RemoteIPType", Lit(null)),
            Map("InitiatingProcessFileName", RegexExtract(JsonText(Col("raw_log"), "$.Application"), @"[^\\]+$", 0)),
            Map("InitiatingProcessFolderPath", JsonText(Col("raw_log"), "$.Application")),
            Map("InitiatingProcessId", Lit(null)),
            Map("InitiatingProcessCommandLine", Lit(null)),
            Map("InitiatingProcessAccountName", Lit(null)),
            Map("InitiatingProcessSHA256", Lit(null)),
            Map("ReportId", JsonText(Col("raw_log"), "$.EventRecordID"))
        ],
        description: "Windows Security 5156 contributor for NetworkSession.");

    public static readonly ParserViewDef NetworkSessionWindowsSysmonEid3 = CreateParser(
        schema: "silver",
        name: "v_networksession_windows_sysmon_eid3",
        sourceName: "Windows Sysmon Event ID 3",
        canonicalTarget: "NetworkSession",
        sourceObject: "bronze.windows_sysmon_event",
        filter: EventIdEquals("3"),
        columns: GoldenEventContracts.NetworkSessionColumns,
        projections:
        [
            Map("Timestamp", EventTimestamp("$.UtcTime")),
            Map("DeviceName", Col("host")),
            Map("ActionType", Lit("ConnectionSuccess")),
            Map("LocalIP", JsonText(Col("raw_log"), "$.SourceIp")),
            Map("LocalPort", TryCast(JsonText(Col("raw_log"), "$.SourcePort"), DuckDbType.Integer)),
            Map("RemoteIP", JsonText(Col("raw_log"), "$.DestinationIp")),
            Map("RemotePort", TryCast(JsonText(Col("raw_log"), "$.DestinationPort"), DuckDbType.Integer)),
            Map("Protocol", JsonText(Col("raw_log"), "$.Protocol")),
            Map("RemoteUrl", JsonText(Col("raw_log"), "$.DestinationHostname")),
            Map("LocalIPType", Lit(null)),
            Map("RemoteIPType", Lit(null)),
            Map("InitiatingProcessFileName", RegexExtract(JsonText(Col("raw_log"), "$.Image"), @"[^\\]+$", 0)),
            Map("InitiatingProcessFolderPath", JsonText(Col("raw_log"), "$.Image")),
            Map("InitiatingProcessId", TryCast(JsonText(Col("raw_log"), "$.ProcessId"), DuckDbType.BigInt)),
            Map("InitiatingProcessCommandLine", Lit(null)),
            Map("InitiatingProcessAccountName", JsonText(Col("raw_log"), "$.User")),
            Map("InitiatingProcessSHA256", JsonText(Col("raw_log"), "$.Hashes")),
            Map("ReportId", JsonText(Col("raw_log"), "$.EventRecordID"))
        ],
        description: "Sysmon Event ID 3 contributor for NetworkSession.");

    public static readonly ParserViewDef ProcessEventWindowsSecurityEid4688 = CreateParser(
        schema: "silver",
        name: "v_processevent_windows_security_eid4688",
        sourceName: "Windows Security Event ID 4688",
        canonicalTarget: "ProcessEvent",
        sourceObject: "bronze.windows_security_event",
        filter: EventIdEquals("4688"),
        columns: GoldenEventContracts.ProcessEventColumns,
        projections:
        [
            Map("Timestamp", EventTimestamp("$.TimeCreated")),
            Map("DeviceId", Lit(null)),
            Map("DeviceName", Col("host")),
            Map("ActionType", Lit("ProcessCreated")),
            Map("FileName", RegexExtract(JsonText(Col("raw_log"), "$.NewProcessName"), @"[^\\]+$", 0)),
            Map("FolderPath", JsonText(Col("raw_log"), "$.NewProcessName")),
            Map("SHA256", Lit(null)),
            Map("ProcessId", TryCast(JsonText(Col("raw_log"), "$.NewProcessId"), DuckDbType.BigInt)),
            Map("ProcessCommandLine", JsonText(Col("raw_log"), "$.CommandLine")),
            Map("AccountName", JsonText(Col("raw_log"), "$.SubjectUserName")),
            Map("InitiatingProcessFileName", RegexExtract(JsonText(Col("raw_log"), "$.ParentProcessName"), @"[^\\]+$", 0)),
            Map("InitiatingProcessCommandLine", Lit(null)),
            Map("ReportId", JsonText(Col("raw_log"), "$.EventRecordID")),
            Map("AdditionalFields", Col("raw_log"))
        ],
        description: "Windows Security 4688 contributor for ProcessEvent.");

    public static readonly ParserViewDef ProcessEventWindowsSysmonEid1 = CreateParser(
                            schema: "silver",
        name: "v_processevent_windows_sysmon_eid1",
        sourceName: "Windows Sysmon Event ID 1",
        canonicalTarget: "ProcessEvent",
        sourceObject: "bronze.windows_sysmon_event",
        filter: EventIdEquals("1"),
        columns: GoldenEventContracts.ProcessEventColumns,
        projections:
        [
            Map("Timestamp", EventTimestamp("$.UtcTime")),
            Map("DeviceId", Lit(null)),
            Map("DeviceName", Col("host")),
            Map("ActionType", Lit("ProcessCreated")),
            Map("FileName", RegexExtract(JsonText(Col("raw_log"), "$.Image"), @"[^\\]+$", 0)),
            Map("FolderPath", JsonText(Col("raw_log"), "$.Image")),
            Map("SHA256", JsonText(Col("raw_log"), "$.Hashes")),
            Map("ProcessId", TryCast(JsonText(Col("raw_log"), "$.ProcessId"), DuckDbType.BigInt)),
            Map("ProcessCommandLine", JsonText(Col("raw_log"), "$.CommandLine")),
            Map("AccountName", JsonText(Col("raw_log"), "$.User")),
            Map("InitiatingProcessFileName", RegexExtract(JsonText(Col("raw_log"), "$.ParentImage"), @"[^\\]+$", 0)),
            Map("InitiatingProcessCommandLine", JsonText(Col("raw_log"), "$.ParentCommandLine")),
            Map("ReportId", JsonText(Col("raw_log"), "$.EventRecordID")),
            Map("AdditionalFields", Col("raw_log"))
        ],
        description: "Sysmon Event ID 1 contributor for ProcessEvent.");

    private static ParserViewDef CreateParser(
        string schema,
        string name,
        string sourceName,
        string canonicalTarget,
        string sourceObject,
        ExprDef filter,
        IReadOnlyList<ColumnDef> columns,
        IReadOnlyList<ProjectionDef> projections,
        string description) =>
        new(
            Schema: schema,
            Name: name,
            SourceName: sourceName,
            CanonicalTarget: canonicalTarget,
            Mapping: new MappingQueryDef(
                SourceObject: sourceObject,
                Filter: filter,
                Projections: projections),
            Columns: columns,
            Description: description);

    private static ExprDef EventIdEquals(string eventId) =>
        And(
            JsonExists(Col("raw_log"), "$.EventID"),
            Eq(JsonText(Col("raw_log"), "$.EventID"), Lit(eventId)));

    private static ExprDef EventTimestamp(string jsonPath) =>
            Fn(
            "COALESCE",
            TryCast(JsonText(Col("raw_log"), jsonPath), DuckDbType.Timestamp),
            Col("ingest_time"));

    private static ExprDef WindowsDnsServerEvtxQuerySelector() =>
            Or(
                EventIdEquals("256"),
                EventIdEquals("257"));
}
