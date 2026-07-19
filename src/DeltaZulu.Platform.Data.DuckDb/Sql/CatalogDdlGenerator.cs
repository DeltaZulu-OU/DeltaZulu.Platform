using DeltaZulu.Platform.Domain.Analytics.Schema.Catalog;
using DeltaZulu.Platform.Domain.Analytics.Schema.Catalog.Projections;

namespace DeltaZulu.Platform.Data.DuckDb.Sql;

/// <summary>
/// Projects a type contract catalog onto DuckDB DDL by reusing <see cref="SchemaEmitter"/> —
/// the catalog supplies columns to the existing emitter, rather than a parallel one (ADR-2).
/// </summary>
public static class CatalogDdlGenerator
{
    public static string EmitCreateTable(SourceFieldCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return new SchemaEmitter().EmitCreateTable(SchemaObjectProjection.ToSilverTableDef(catalog));
    }
}
