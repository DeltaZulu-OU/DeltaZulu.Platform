namespace Hunting.Schema.Definitions.Medallion;

using Hunting.Core.Schema;
using Hunting.Schema.Definitions.Medallion.Bronze;
using Hunting.Schema.Definitions.Medallion.Golden;
using Hunting.Schema.Definitions.Medallion.Silver;

/// <summary>
/// C#-first catalog for the medallion schema path.
///
/// Follow-up commits add source/event-specific Silver filters, parser-specific
/// JSON extraction, and schema-pipeline wiring in small reviewable slices.
/// </summary>
public static class MedallionSchemaCatalog
{
    /// <summary>
    /// Source-preserving Bronze ingestion tables.
    /// </summary>
    public static IReadOnlyList<RawTableDef> RawTables { get; } =
    [
        BronzeSourceTables.WindowsSysmonEvent,
        BronzeSourceTables.WindowsSecurityEvent,
        BronzeSourceTables.DnsServerEvent
    ];

    /// <summary>
    /// Source/event-specific Silver parser views.
    /// </summary>
    public static IReadOnlyList<ParserViewDef> ParserViews { get; } =
    [
        SilverParserContributors.ProcessEventWindowsSysmonEid1,
        SilverParserContributors.ProcessEventWindowsSecurityEid4688,
        SilverParserContributors.NetworkSessionWindowsSysmonEid3,
        SilverParserContributors.NetworkSessionWindowsSecurityEid5156,
        SilverParserContributors.DnsWindowsSysmonEid22,
        SilverParserContributors.DnsServerQueryEvent
    ];

    /// <summary>
    /// Operator-facing Golden hunting views.
    /// </summary>
    public static IReadOnlyList<CanonicalViewDef> CanonicalViews { get; } =
    [
        GoldenEventContracts.Dns,
        GoldenEventContracts.NetworkSession,
        GoldenEventContracts.ProcessEvent
    ];
}
