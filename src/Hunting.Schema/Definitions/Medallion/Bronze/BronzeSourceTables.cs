namespace Hunting.Schema.Definitions.Medallion.Bronze;

using Hunting.Core.Schema;

/// <summary>
/// Active Phase 1A source-family Bronze table definitions.
///
/// Bronze preserves source-shaped records with minimal interpretation. Source/event
/// interpretation belongs in Silver parser views.
/// </summary>
public static class BronzeSourceTables
{
    public static readonly IReadOnlyList<ColumnDef> SourceRecordColumns =
    [
        new("ingest_time", DuckDbType.Timestamp, KustoType.DateTime, Description: "Time the record was ingested into the local store."),
        new("source_name", DuckDbType.Varchar, KustoType.String, Description: "Logical source family or collector name."),
        new("provider", DuckDbType.Varchar, KustoType.String, Description: "Source provider or product identifier."),
        new("host", DuckDbType.Varchar, KustoType.String, Description: "Host or device name when available."),
        new("raw_log", DuckDbType.Json, KustoType.Dynamic, Description: "Source-shaped raw event payload."),
        new("raw_text", DuckDbType.Varchar, KustoType.String, Description: "Optional original text representation.")
    ];

    public static readonly RawTableDef WindowsSysmonEvent = new(
        Schema: "bronze",
        Name: "windows_sysmon_event",
        Columns: SourceRecordColumns,
        SourceDescription: "Windows Sysmon source-shaped event records",
        Description: "Source-preserving Bronze table for Windows Sysmon records.");

    public static readonly RawTableDef WindowsSecurityEvent = new(
        Schema: "bronze",
        Name: "windows_security_event",
        Columns: SourceRecordColumns,
        SourceDescription: "Windows Security Event Log source-shaped records",
        Description: "Source-preserving Bronze table for Windows Security records.");

    public static readonly RawTableDef DnsServerEvent = new(
        Schema: "bronze",
        Name: "dns_server_event",
        Columns: SourceRecordColumns,
        SourceDescription: "DNS server source-shaped event records",
        Description: "Source-preserving Bronze table for DNS server records.");
}