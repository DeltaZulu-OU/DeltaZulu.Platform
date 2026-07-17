using DeltaZulu.Platform.Domain.Analytics.Schema.Definitions.Internal;
using DeltaZulu.Platform.Domain.Analytics.Schema.Definitions.Medallion;

namespace DeltaZulu.Platform.Domain.Analytics.Schema;

/// <summary>
/// Central schema conventions and bootstrap contracts for the hunting model.
/// This keeps schema-surface ownership in Analytics.Schema instead of UI composition code.
/// </summary>
public static class SchemaConventions
{
    public const string BronzeSchema = "bronze";
    public const string SilverSchema = "silver";
    public const string GoldenSchema = "golden";
    public const string InternalSchema = "internal";

    public static IReadOnlyList<RawTableDef> RawTables => MedallionSchemaCatalog.RawTables;

    public static IReadOnlyList<InternalTableDef> InternalTables =>
    [
        .. InternalSchemaCatalog.InternalTables,
        OperationsSchemaContracts.AlertEventsTable,
        OperationsSchemaContracts.AlertEntitiesTable
    ];

    public static IReadOnlyList<InternalViewDef> InternalViews => InternalSchemaCatalog.InternalViews;

    public static IReadOnlyList<ParserViewDef> ParserViews => MedallionSchemaCatalog.ParserViews;

    public static IReadOnlyList<CanonicalViewDef> CanonicalViews =>
    [
        .. MedallionSchemaCatalog.CanonicalViews,
        OperationsSchemaContracts.AlertEvent,
        OperationsSchemaContracts.AlertEntity
    ];

    /// <summary>
    /// UI-agnostic editor metadata projected from the same Golden contracts used
    /// for SQL view generation. Hosts can serialize this without referencing Web.
    /// </summary>
    public static EditorSchemaMetadata EditorMetadata
        => EditorSchemaMetadata.FromCanonicalViews(CanonicalViews);
}
