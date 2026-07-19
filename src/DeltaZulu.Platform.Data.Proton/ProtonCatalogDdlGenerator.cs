using DeltaZulu.Platform.Domain.Analytics.Schema.Catalog;
using DeltaZulu.Platform.Domain.Analytics.Schema.Catalog.Projections;

namespace DeltaZulu.Platform.Data.Proton;

/// <summary>
/// Projects a type contract catalog onto Proton DDL by reusing <see cref="ProtonSchemaEmitter"/> —
/// the catalog supplies columns to the existing emitter, rather than a parallel one (ADR-2).
/// </summary>
public static class ProtonCatalogDdlGenerator
{
    public static string EmitStream(SourceFieldCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return new ProtonSchemaEmitter().EmitStream(SchemaObjectProjection.ToSilverTableDef(catalog));
    }
}
