namespace Hunting.Schema;

using Hunting.Core.Catalog;
using Hunting.Core.Schema;
using Definitions;

/// <summary>
/// Central medallion schema conventions and bootstrap contracts for the hunting model.
/// This keeps schema-surface ownership in Hunting.Schema instead of UI composition code.
/// </summary>
public static class SchemaConventions
{
    public const string BronzeSchema = "bronze";
    public const string SilverSchema = "silver";
    public const string GoldenSchema = "golden";

    public static IReadOnlyList<RawTableDef> RawTables => [DeviceProcessEventsSchema.RawWindowsEventJson];

    public static IReadOnlyList<ParserViewDef> ParserViews =>
    [
        DeviceProcessEventsSchema.SysmonProcessCreate,
        DeviceNetworkEventsSchema.SysmonNetworkConnect
    ];

    public static IReadOnlyList<CanonicalViewDef> CanonicalViews =>
    [
        DeviceProcessEventsSchema.View,
        DeviceNetworkEventsSchema.View
    ];

    public static void RegisterCanonicalViews(ApprovedViewCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        foreach (var view in CanonicalViews)
        {
            catalog.Register(view);
        }
    }
}
