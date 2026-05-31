namespace Hunting.Schema.Definitions.Phase1D;

using Hunting.Core.Schema;

/// <summary>
/// Phase 1D parser specification catalog for the active medallion Silver contributors.
/// This is a reviewable specification layer over the existing ParserViewDef catalog.
/// It does not yet replace parser-view generation.
/// </summary>
public static class Phase1DParserSpecCatalog
{
    public static IReadOnlyList<ParserSpec> ParserSpecs { get; } =
        SchemaConventions.ParserViews
            .Select(ToParserSpec)
            .OrderBy(static spec => spec.QualifiedName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool HasColumn(CanonicalViewDef view, string columnName) =>
        view.Columns.Any(column => column.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));

    private static ParserSpec ToParserSpec(ParserViewDef view)
    {
        var target = SchemaConventions.CanonicalViews.Single(canonical =>
            canonical.QualifiedName.Equals(view.CanonicalTarget, StringComparison.OrdinalIgnoreCase) ||
            canonical.Name.Equals(view.CanonicalTarget, StringComparison.OrdinalIgnoreCase));

        var targetColumnNames = target.Columns
            .Select(static column => column.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var projections = view.Columns
            .Where(column => targetColumnNames.Contains(column.Name))
            .Select(static column => new ParserProjectionSpec(
                targetColumn: column.Name,
                expression: $"existing:{column.Name}",
                sourceField: column.Name,
                kind: ParserProjectionKind.Expression))
            .ToArray();

        var suppliedColumnNames = projections
            .Select(static projection => projection.TargetColumn)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var intentionalNulls = target.Columns
            .Where(column => !suppliedColumnNames.Contains(column.Name))
            .Select(static column => new ParserIntentionalNullSpec(
                targetColumn: column.Name,
                duckDbType: column.DuckDbType,
                reason: "Column is not supplied by the current ParserViewDef branch."))
            .ToArray();

        return new ParserSpec(
            name: view.QualifiedName,
            sourceObject: view.Mapping.SourceObject,
            targetContract: target.QualifiedName,
            sourceName: view.SourceName,
            selector: "Existing ParserViewDef selector",
            projections: projections,
            intentionalNulls: intentionalNulls,
            additionalFieldsPolicy: HasColumn(target, "AdditionalFields")
                ? ParserAdditionalFieldsPolicy.PreserveRawLog
                : ParserAdditionalFieldsPolicy.None);
    }
}