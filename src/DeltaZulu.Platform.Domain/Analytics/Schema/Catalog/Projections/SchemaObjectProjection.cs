namespace DeltaZulu.Platform.Domain.Analytics.Schema.Catalog.Projections;

/// <summary>
/// Wraps a catalog's columns in the existing <see cref="SchemaObjectDef"/> model so both the
/// DuckDB and Proton DDL emitters — which neither depend on each other nor should duplicate
/// this wrapping — can consume the same catalog-derived table shape.
/// </summary>
public static class SchemaObjectProjection
{
    /// <summary>
    /// A catalog describes a per-source-family Silver record: promoted common fields plus
    /// whatever stays in <see cref="KustoType.Dynamic"/> bag columns (ADR 0007's target Silver
    /// shape). It is never Bronze (which stays untyped raw payload) or Golden (which is a
    /// semantic mapping this catalog explicitly does not perform — see ADR-2/ADR-1).
    /// </summary>
    public static InternalTableDef ToSilverTableDef(SourceFieldCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return new InternalTableDef(
            Schema: "silver",
            Name: catalog.Source,
            Columns: CatalogColumnProjection.ToColumns(catalog),
            Description: $"Type-contract-catalog-derived Silver record for source '{catalog.Source}'.");
    }
}
