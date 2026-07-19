namespace DeltaZulu.Platform.Domain.Analytics.Schema.Catalog.Projections;

/// <summary>
/// Projects a <see cref="SourceFieldCatalog"/> onto the existing <see cref="ColumnDef"/> model,
/// so the existing DuckDB/Proton DDL emitters — the sole authority for those dialects per ADR 0007
/// — can be reused unchanged rather than duplicated for catalog-sourced tables.
/// </summary>
public static class CatalogColumnProjection
{
    public static IReadOnlyList<ColumnDef> ToColumns(SourceFieldCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return catalog.Entries.Select(ToColumn).ToList();
    }

    private static ColumnDef ToColumn(SourceFieldCatalogEntry entry)
    {
        var duckDbType = entry is { Annotation: { Kind: FieldAnnotationKind.Decimal } }
            ? DuckDbType.Double // DuckDB DDL keeps the existing (lossy) Decimal->Double default; true precision is preserved in the Avro/Arrow projections instead. See ADR-2.
            : entry.KustoType.ToDefaultDuckDbType();

        return new ColumnDef(
            entry.Name,
            duckDbType,
            entry.KustoType,
            Nullable: true,
            Description: DescribeAnnotation(entry));
    }

    private static string? DescribeAnnotation(SourceFieldCatalogEntry entry)
        => entry.Annotation switch {
            null => null,
            { Kind: FieldAnnotationKind.Duration } a => $"duration ({a.Unit}); grammar: {entry.ParserGrammarRef}",
            { Kind: FieldAnnotationKind.NestedPath } a => $"documented, intentionally not promoted from {a.Path}",
            { Kind: var kind } => $"{kind}; grammar: {entry.ParserGrammarRef}"
        };
}
