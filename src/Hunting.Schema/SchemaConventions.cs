namespace Hunting.Schema;

using Hunting.Core.Catalog;
using Hunting.Core.Schema;
using Hunting.Schema.Definitions.Medallion;

/// <summary>
/// Central medallion schema conventions and bootstrap contracts for the hunting model.
/// This keeps schema-surface ownership in Hunting.Schema instead of UI composition code.
/// </summary>
public static class SchemaConventions
{
    public const string BronzeSchema = "bronze";
    public const string SilverSchema = "silver";
    public const string GoldenSchema = "golden";

    public static IReadOnlyList<RawTableDef> RawTables => MedallionSchemaCatalog.RawTables;

    public static IReadOnlyList<ParserViewDef> ParserViews => MedallionSchemaCatalog.ParserViews;

    public static IReadOnlyList<CanonicalViewDef> CanonicalViews => MedallionSchemaCatalog.CanonicalViews;

    public static void RegisterCanonicalViews(ApprovedViewCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        foreach (var view in CanonicalViews)
        {
            catalog.Register(view);
        }
    }
}
