namespace Hunting.Schema.Definitions.Medallion.Silver;

using Hunting.Core.Mapping;
using Hunting.Core.Schema;
using Hunting.Schema.Definitions.Medallion.Golden;
using static Hunting.Core.Mapping.MapDsl;

/// <summary>
/// Active Phase 1A Silver parser contributors.
///
/// This commit maps the DNS server query contributor. At this point all six
/// active Phase 1A Silver contributors have source/event filters and parser
/// projections. Semantic normalization remains a later hardening step.
/// </summary>
public static class SilverParserContributors
{
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
            Map("Timestamp", Col("ingest_time")),
            Map("DeviceId", Lit(null)),
            Map("DeviceName", Col("host")),
            Map("ActionType", Lit("ProcessCreated")),
            Map("FileName", RegexExtract(JsonText(Col("raw_log"), "$.Image"), @"[^\\]+$", 0)),
            Map("FolderPath", JsonText(Col("raw_log"), "$.Image")),
            Map("SHA256", JsonText(Col("raw_log"), "$.Hashes")),
            Map("ProcessId", Cast(JsonText(Col("raw_log"), "$.ProcessId"), DuckDbType.BigInt)),
            Map("ProcessCommandLine", JsonText(Col("raw_log"), "$.CommandLine")),
            Map("AccountName", JsonText(Col("raw_log"), "$.User")),
            Map("InitiatingProcessFileName", RegexExtract(JsonText(Col("raw_log"), "$.ParentImage"), @"[^\\]+$", 0)),
            Map("InitiatingProcessCommandLine", JsonText(Col("raw_log"), "$.ParentCommandLine")),
            Map("ReportId", JsonText(Col("raw_log"), "$.EventRecordID")),
            Map("AdditionalFields", Col("raw_log"))
        ],
        description: "Sysmon Event ID 1 contributor for ProcessEvent.");

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
            Map("Timestamp", Col("ingest_time")),
            Map("DeviceId", Lit(null)),
            Map("DeviceName", Col("host")),
            Map("ActionType", Lit("ProcessCreated")),
            Map("FileName", RegexExtract(JsonText(Col("raw_log"), "$.NewProcessName"), @"[^\\]+$", 0)),
            Map("FolderPath", JsonText(Col("raw_log"), "$.NewProcessName")),
            Map("SHA256", Lit(null)),
            Map("ProcessId", Lit(null)),
            Map("ProcessCommandLine", JsonText(Col("raw_log"), "$.CommandLine")),
            Map("AccountName", JsonText(Col("raw_log"), "$.SubjectUserName")),
            Map("InitiatingProcessFileName", RegexExtract(JsonText(Col("raw_log"), "$.ParentProcessName"), @"[^\\]+$", 0)),
            Map("InitiatingProcessCommandLine", Lit(null)),
            Map("ReportId", JsonText(Col("raw_log"), "$.EventRecordID")),
            Map("AdditionalFields", Col("raw_log"))
        ],
        description: "Windows Security 4688 contributor for ProcessEvent.");

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
            Map("Timestamp", Col("ingest_time")),
            Map("DeviceName", Col("host")),
            Map("ActionType", Lit("ConnectionSuccess")),
            Map("LocalIP", JsonText(Col("raw_log"), "$.SourceIp")),
            Map("LocalPort", Cast(JsonText(Col("raw_log"), "$.SourcePort"), DuckDbType.Integer)),
            Map("RemoteIP", JsonText(Col("raw_log"), "$.DestinationIp")),
            Map("RemotePort", Cast(JsonText(Col("raw_log"), "$.DestinationPort"), DuckDbType.Integer)),
            Map("Protocol", JsonText(Col("raw_log"), "$.Protocol")),
            Map("RemoteUrl", JsonText(Col("raw_log"), "$.DestinationHostname")),
            Map("LocalIPType", Lit(null)),
            Map("RemoteIPType", Lit(null)),
            Map("InitiatingProcessFileName", RegexExtract(JsonText(Col("raw_log"), "$.Image"), @"[^\\]+$", 0)),
            Map("InitiatingProcessFolderPath", JsonText(Col("raw_log"), "$.Image")),
            Map("InitiatingProcessId", Cast(JsonText(Col("raw_log"), "$.ProcessId"), DuckDbType.BigInt)),
            Map("InitiatingProcessCommandLine", Lit(null)),
            Map("InitiatingProcessAccountName", JsonText(Col("raw_log"), "$.User")),
            Map("InitiatingProcessSHA256", JsonText(Col("raw_log"), "$.Hashes")),
            Map("ReportId", JsonText(Col("raw_log"), "$.EventRecordID"))
        ],
        description: "Sysmon Event ID 3 contributor for NetworkSession.");

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
            Map("Timestamp", Col("ingest_time")),
            Map("DeviceName", Col("host")),
            Map("ActionType", Lit("ConnectionAllowed")),
            Map("LocalIP", JsonText(Col("raw_log"), "$.SourceAddress")),
            Map("LocalPort", Lit(null)),
            Map("RemoteIP", JsonText(Col("raw_log"), "$.DestAddress")),
            Map("RemotePort", Lit(null)),
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
            Map("Timestamp", Col("ingest_time")),
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

    public static readonly ParserViewDef DnsServerQueryEvent = CreateParser(
        schema: "silver",
        name: "v_dns_server_query_event",
        sourceName: "DNS server query event",
        canonicalTarget: "Dns",
        sourceObject: "bronze.dns_server_event",
        filter: OpcodeEquals("QUERY"),
        columns: GoldenEventContracts.DnsColumns,
        projections:
        [
            Map("Timestamp", Col("ingest_time")),
            Map("DeviceName", Col("host")),
            Map("ActionType", Lit("DnsQuery")),
            Map("QueryName", JsonText(Col("raw_log"), "$.query_name")),
            Map("QueryType", JsonText(Col("raw_log"), "$.query_type")),
            Map("ResponseCode", JsonText(Col("raw_log"), "$.response_code")),
            Map("ResponseName", JsonText(Col("raw_log"), "$.response_name")),
            Map("ResponseIP", JsonText(Col("raw_log"), "$.response_ip")),
            Map("SrcIpAddr", JsonText(Col("raw_log"), "$.client_ip")),
            Map("SrcPortNumber", Lit(null)),
            Map("Protocol", JsonText(Col("raw_log"), "$.protocol")),
            Map("ReportId", JsonText(Col("raw_log"), "$.opcode")),
            Map("AdditionalFields", Col("raw_log"))
        ],
        description: "DNS server query contributor for Dns.");

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

    private static ExprDef OpcodeEquals(string opcode) =>
        And(
            JsonExists(Col("raw_log"), "$.opcode"),
            Eq(JsonText(Col("raw_log"), "$.opcode"), Lit(opcode)));
}
