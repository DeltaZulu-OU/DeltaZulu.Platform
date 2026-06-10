namespace DeltaZulu.Hunting.Core.Schema;

/// <summary>
/// UI-agnostic editor metadata generated from the public C# schema contracts.
/// Consumers such as Monaco integrations can serialize this model without
/// depending on the Web project or duplicating schema text in JavaScript.
/// </summary>
public sealed record EditorSchemaMetadata(
    IReadOnlyList<EditorTableMetadata> Tables,
    EditorLanguageMetadata Language)
{
    /// <summary>
    /// Projects the approved Golden hunting views into editor-facing metadata.
    /// Bronze and Silver objects are intentionally not accepted by this API.
    /// </summary>
    public static EditorSchemaMetadata FromCanonicalViews(IEnumerable<CanonicalViewDef> views)
    {
        ArgumentNullException.ThrowIfNull(views);

        return new EditorSchemaMetadata(
            views.Select(view => new EditorTableMetadata(
                    view.Name,
                    view.Description,
                    view.ParserViews.ToArray(),
                    view.Columns.Select(column => new EditorColumnMetadata(
                            column.Name,
                            column.KustoType.ToKustoName(),
                            column.Nullable,
                            column.KustoType == KustoType.Dynamic,
                            column.Description))
                        .ToArray()))
                .ToArray(),
            EditorLanguageMetadata.Supported);
    }
}

/// <summary>
/// Editor language terms for the supported KQL subset. Keep this list aligned
/// with the translation checklist so UI suggestions do not advertise deferred
/// constructs that the backend rejects.
/// </summary>
public sealed record EditorLanguageMetadata(
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> Operators,
    IReadOnlyList<string> RenderKinds)
{
    public static EditorLanguageMetadata Supported { get; } = new(
        Keywords: Array.AsReadOnly(
        [
            "where", "project", "take", "limit", "extend", "summarize",
            "by", "join", "on", "kind", "lookup", "sort", "order", "asc", "desc",
            "count", "distinct", "top", "print", "sample", "sample-distinct", "render"
        ]),
        Operators: Array.AsReadOnly(
        [
            "==", "!=", "=~", "!~", ">", "<", ">=", "<=",
            "between", "!between", "in", "!in", "and", "or", "not",
            "contains", "!contains", "contains_cs", "!contains_cs",
            "startswith", "!startswith", "startswith_cs", "!startswith_cs",
            "endswith", "!endswith", "endswith_cs", "!endswith_cs",
            "has", "!has", "has_cs", "!has_cs",
            "hasprefix", "!hasprefix", "hasprefix_cs", "!hasprefix_cs",
            "hassuffix", "!hassuffix", "hassuffix_cs", "!hassuffix_cs"
        ]),
        RenderKinds: Array.AsReadOnly(
        [
            "table", "timechart", "linechart", "barchart", "columnchart",
            "piechart", "areachart", "scatterchart"
        ]));
}

/// <summary>
/// Editor metadata for one public hunting table.
/// </summary>
public sealed record EditorTableMetadata(
    string Name,
    string? Description,
    IReadOnlyList<string> Contributors,
    IReadOnlyList<EditorColumnMetadata> Columns);

/// <summary>
/// Editor metadata for one public hunting column.
/// </summary>
public sealed record EditorColumnMetadata(
    string Name,
    string Type,
    bool Nullable,
    bool Dynamic,
    string? Description);