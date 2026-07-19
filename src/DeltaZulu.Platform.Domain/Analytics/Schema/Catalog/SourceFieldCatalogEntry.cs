using System.Text.RegularExpressions;

namespace DeltaZulu.Platform.Domain.Analytics.Schema.Catalog;

/// <summary>
/// One row of the type contract catalog: everything known about a single field
/// emitted by a parser for one source. <see cref="KustoType"/> is the author-facing
/// contract (what a KQL query sees); <see cref="Annotation"/> is the translator-facing
/// contract consumed by the Avro/Arrow/Proton/DuckDB projections.
/// </summary>
public sealed partial record SourceFieldCatalogEntry
{
    /// <summary>Translator-facing logical annotation refining <see cref="KustoType"/>. Null when the scalar is self-describing.</summary>
    public FieldAnnotation? Annotation { get; }

    /// <summary>The single canonical form this field's value is normalized into at parse time.</summary>
    public CanonicalizationPolicy Canonicalization { get; }

    /// <summary>The emitted field name, as it appears in parser output and downstream columns.</summary>
    public string Name { get; }

    /// <summary>
    /// Reference into the parser grammar that produces this field (e.g. a rulebase rule id
    /// or, for structured motifs, the extension/sub-key path within that motif). This is the
    /// catalog's link back to DeltaZulu.Parse; it is not itself a JSON path into a bag.
    /// </summary>
    public string ParserGrammarRef { get; }

    /// <summary>
    /// Whether this field, though sourced from inside a <see cref="KustoType.Dynamic"/> bag on
    /// this source, is promoted to a typed top-level column rather than left in the bag.
    /// </summary>
    public bool Promoted { get; }

    /// <summary>The author-facing KQL scalar type. Never <see cref="KustoType.Int"/> — the catalog collapses int into <see cref="KustoType.Long"/> (ADR-2).</summary>
    public KustoType KustoType { get; }

    /// <summary>
    /// Reserved hook for the future semantic-normalization layer (ADR-1 in DeltaZulu.Parse,
    /// ADR-5 pending). Always null in this phase; the column exists so downstream tooling has
    /// a stable place to look, and does not need to be added later.
    /// </summary>
    public string? Semantic { get; }

    public SourceFieldCatalogEntry(
        string name,
        KustoType kustoType,
        string parserGrammarRef,
        FieldAnnotation? annotation = null,
        CanonicalizationPolicy canonicalization = CanonicalizationPolicy.None,
        bool promoted = false,
        string? semantic = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(parserGrammarRef);

        if (!FieldNamePattern().IsMatch(name))
        {
            throw new ArgumentException($"Field name '{name}' must be a lowerCamelCase-or-snake_case identifier.", nameof(name));
        }

        if (kustoType == KustoType.Int)
        {
            throw new ArgumentException(
                "The catalog collapses int into long (ADR-2); use KustoType.Long.", nameof(kustoType));
        }

        if (annotation is { Kind: FieldAnnotationKind.NestedPath } && promoted)
        {
            throw new ArgumentException(
                "A field annotated NestedPath is documented as intentionally left in its bag; it cannot also be Promoted.",
                nameof(promoted));
        }

        if (kustoType == KustoType.Decimal && annotation is not { Kind: FieldAnnotationKind.Decimal })
        {
            throw new ArgumentException(
                "KustoType.Decimal requires a Decimal annotation with explicit precision/scale. " +
                "Without it, the Avro/Arrow projections would silently fall back to a lossy double " +
                "rather than a fidelity-preserving decimal type.",
                nameof(annotation));
        }

        Name = name.Trim();
        KustoType = kustoType;
        ParserGrammarRef = parserGrammarRef.Trim();
        Annotation = annotation;
        Canonicalization = canonicalization;
        Promoted = promoted;
        Semantic = string.IsNullOrWhiteSpace(semantic) ? null : semantic.Trim();
    }

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9_]*$")]
    private static partial Regex FieldNamePattern();
}
