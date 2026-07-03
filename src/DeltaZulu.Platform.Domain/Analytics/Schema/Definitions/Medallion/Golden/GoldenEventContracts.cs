namespace DeltaZulu.Platform.Domain.Analytics.Schema.Definitions.Medallion.Golden;

/// <summary>
/// <para>Active Phase 1A Golden operator-facing contracts.</para>
/// <para>These contracts intentionally use singular event-family names.</para>
/// </summary>
public static class GoldenEventContracts
{
    public static readonly IReadOnlyList<ColumnDef> DnsColumns =
    [
        new("Timestamp", DuckDbType.Timestamp, KustoType.DateTime, Description: "Event time."),
        new("DeviceName", DuckDbType.Varchar, KustoType.String, Description: "Host or resolver name."),
        new("ActionType", DuckDbType.Varchar, KustoType.String, Description: "Canonical DNS action."),
        new("QueryName", DuckDbType.Varchar, KustoType.String, Description: "Queried DNS name."),
        new("QueryType", DuckDbType.Varchar, KustoType.String, Description: "DNS query type."),
        new("ResponseCode", DuckDbType.Varchar, KustoType.String, Description: "DNS response code or status."),
        new("ResponseName", DuckDbType.Varchar, KustoType.String, Description: "Returned DNS name when available."),
        new("ResponseIP", DuckDbType.Varchar, KustoType.String, Description: "Returned IP address when available."),
        new("SrcIpAddr", DuckDbType.Varchar, KustoType.String, Description: "Client source IP address."),
        new("SrcPortNumber", DuckDbType.Integer, KustoType.Int, Description: "Client source port."),
        new("Protocol", DuckDbType.Varchar, KustoType.String, Description: "Transport protocol."),
        new("ReportId", DuckDbType.Varchar, KustoType.String, Description: "Source report or event identifier."),
        new("AdditionalFields", DuckDbType.Json, KustoType.Dynamic, Description: "Source-specific additional data.")
    ];

    public static readonly CanonicalViewDef Dns = new(
        Schema: "golden",
        Name: "Dns",
        ParserViews:
        [
            "silver.v_dns_windows_sysmon_eid22",
            "silver.v_dns_server_query_event"
        ],
        Columns: DnsColumns!,
        Description: "DNS query and response events across configured sources.");

    public static readonly IReadOnlyList<ColumnDef> NetworkSessionColumns =
    [
        new("Timestamp", DuckDbType.Timestamp, KustoType.DateTime, Description: "Event time."),
        new("DeviceName", DuckDbType.Varchar, KustoType.String, Description: "Host or device name."),
        new("ActionType", DuckDbType.Varchar, KustoType.String, Description: "Canonical network action."),
        new("LocalIP", DuckDbType.Varchar, KustoType.String, Description: "Local IP address."),
        new("LocalPort", DuckDbType.Integer, KustoType.Int, Description: "Local port."),
        new("RemoteIP", DuckDbType.Varchar, KustoType.String, Description: "Remote IP address."),
        new("RemotePort", DuckDbType.Integer, KustoType.Int, Description: "Remote port."),
        new("Protocol", DuckDbType.Varchar, KustoType.String, Description: "Transport or network protocol."),
        new("RemoteUrl", DuckDbType.Varchar, KustoType.String, Description: "Remote hostname or URL when available."),
        new("LocalIPType", DuckDbType.Varchar, KustoType.String, Description: "Local IP classification when available."),
        new("RemoteIPType", DuckDbType.Varchar, KustoType.String, Description: "Remote IP classification when available."),
        new("InitiatingProcessFileName", DuckDbType.Varchar, KustoType.String, Description: "Initiating process file name."),
        new("InitiatingProcessFolderPath", DuckDbType.Varchar, KustoType.String, Description: "Initiating process path."),
        new("InitiatingProcessId", DuckDbType.BigInt, KustoType.Long, Description: "Initiating process identifier."),
        new("InitiatingProcessCommandLine", DuckDbType.Varchar, KustoType.String, Description: "Initiating process command line."),
        new("InitiatingProcessAccountName", DuckDbType.Varchar, KustoType.String, Description: "Account associated with the initiating process."),
        new("InitiatingProcessSHA256", DuckDbType.Varchar, KustoType.String, Description: "Initiating process SHA-256 when available."),
        new("ReportId", DuckDbType.Varchar, KustoType.String, Description: "Source report or event identifier.")
    ];

    public static readonly CanonicalViewDef NetworkSession = new(
        Schema: "golden",
        Name: "NetworkSession",
        ParserViews:
        [
            "silver.v_networksession_windows_sysmon_eid3",
            "silver.v_networksession_windows_security_eid5156"
        ],
        Columns: NetworkSessionColumns!,
        Description: "Network session or connection events across configured sources.");

    public static readonly IReadOnlyList<ColumnDef> ProcessEventColumns =
                     [
     new("Timestamp", DuckDbType.Timestamp, KustoType.DateTime, Description: "Event time."),
        new("DeviceId", DuckDbType.Varchar, KustoType.String, Description: "Device identifier when available."),
        new("DeviceName", DuckDbType.Varchar, KustoType.String, Description: "Host or device name."),
        new("ActionType", DuckDbType.Varchar, KustoType.String, Description: "Canonical process action."),
        new("FileName", DuckDbType.Varchar, KustoType.String, Description: "Process image file name."),
        new("FolderPath", DuckDbType.Varchar, KustoType.String, Description: "Process image path."),
        new("SHA256", DuckDbType.Varchar, KustoType.String, Description: "Process image SHA-256 when available."),
        new("ProcessId", DuckDbType.BigInt, KustoType.Long, Description: "Process identifier."),
        new("ProcessCommandLine", DuckDbType.Varchar, KustoType.String, Description: "Process command line."),
        new("AccountName", DuckDbType.Varchar, KustoType.String, Description: "Account name associated with the event."),
        new("InitiatingProcessFileName", DuckDbType.Varchar, KustoType.String, Description: "Parent or initiating process file name."),
        new("InitiatingProcessCommandLine", DuckDbType.Varchar, KustoType.String, Description: "Parent or initiating process command line."),
        new("ReportId", DuckDbType.Varchar, KustoType.String, Description: "Source report or event identifier."),
        new("AdditionalFields", DuckDbType.Json, KustoType.Dynamic, Description: "Source-specific additional data.")
 ];

    public static readonly CanonicalViewDef ProcessEvent = new(
           Schema: "golden",
           Name: "ProcessEvent",
           ParserViews:
           [
               "silver.v_processevent_windows_sysmon_eid1",
            "silver.v_processevent_windows_security_eid4688"
           ],
           Columns: ProcessEventColumns!,
           Description: "Process execution and related process events across configured sources.");

    public static readonly IReadOnlyList<ColumnDef> AuthenticationColumns =
    [
        new("Timestamp", DuckDbType.Timestamp, KustoType.DateTime, Description: "Event time."),
        new("DeviceName", DuckDbType.Varchar, KustoType.String, Description: "Host or device name."),
        new("ActionType", DuckDbType.Varchar, KustoType.String, Description: "Canonical authentication action."),
        new("EventId", DuckDbType.Varchar, KustoType.String, Description: "Windows Security Event ID."),
        new("AccountName", DuckDbType.Varchar, KustoType.String, Description: "Target account name."),
        new("AccountDomain", DuckDbType.Varchar, KustoType.String, Description: "Target account domain."),
        new("SubjectUserName", DuckDbType.Varchar, KustoType.String, Description: "Subject account name."),
        new("SubjectDomainName", DuckDbType.Varchar, KustoType.String, Description: "Subject account domain."),
        new("SubjectUserSid", DuckDbType.Varchar, KustoType.String, Description: "Subject user SID."),
        new("SubjectUserSid_resolved", DuckDbType.Varchar, KustoType.String, Description: "Subject user SID resolved to well-known name."),
        new("TargetUserSid", DuckDbType.Varchar, KustoType.String, Description: "Target user SID."),
        new("TargetUserSid_resolved", DuckDbType.Varchar, KustoType.String, Description: "Target user SID resolved to well-known name."),
        new("LogonType", DuckDbType.Varchar, KustoType.String, Description: "Logon type numeric code."),
        new("LogonType_resolved", DuckDbType.Varchar, KustoType.String, Description: "Logon type resolved to canonical name."),
        new("Status", DuckDbType.Varchar, KustoType.String, Description: "NTSTATUS code."),
        new("Status_resolved", DuckDbType.Varchar, KustoType.String, Description: "NTSTATUS code resolved to symbolic name."),
        new("SubStatus", DuckDbType.Varchar, KustoType.String, Description: "NTSTATUS sub-status code."),
        new("SubStatus_resolved", DuckDbType.Varchar, KustoType.String, Description: "NTSTATUS sub-status resolved to symbolic name."),
        new("SourceIP", DuckDbType.Varchar, KustoType.String, Description: "Source IP address."),
        new("SourcePort", DuckDbType.Varchar, KustoType.String, Description: "Source port."),
        new("WorkstationName", DuckDbType.Varchar, KustoType.String, Description: "Source workstation name."),
        new("AuthenticationPackageName", DuckDbType.Varchar, KustoType.String, Description: "Authentication package used."),
        new("FailureReason", DuckDbType.Varchar, KustoType.String, Description: "Failure reason message resource ID."),
        new("FailureReason_resolved", DuckDbType.Varchar, KustoType.String, Description: "Failure reason resolved to text."),
        new("ReportId", DuckDbType.Varchar, KustoType.String, Description: "Source report or event identifier."),
        new("AdditionalFields", DuckDbType.Json, KustoType.Dynamic, Description: "Source-specific additional data.")
    ];

    public static readonly CanonicalViewDef Authentication = new(
        Schema: "golden",
        Name: "Authentication",
        ParserViews:
        [
            "silver.v_authentication_windows_security_eid4624",
            "silver.v_authentication_windows_security_eid4625"
        ],
        Columns: AuthenticationColumns!,
        Description: "Authentication logon and logon-failure events across configured sources.");
}