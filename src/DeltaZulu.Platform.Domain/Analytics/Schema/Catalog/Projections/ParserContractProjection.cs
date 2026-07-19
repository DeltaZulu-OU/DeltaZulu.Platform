using System.Text.Json;
using System.Text.Json.Nodes;

namespace DeltaZulu.Platform.Domain.Analytics.Schema.Catalog.Projections;

/// <summary>
/// <para>
/// Projection five: the contract a rulebase-suggestion tool (DeltaZulu.Parse's planned
/// Suggester, not yet built — see that repo's <c>LiblognormParserCatalog</c>/
/// <c>LiblognormParserDescriptor</c> groundwork) emits for a human to curate into a full
/// catalog entry.
/// </para>
/// <para>
/// This is deliberately a <em>subset</em> of a full <see cref="SourceFieldCatalogEntry"/>:
/// a Suggester can infer a field's name, closed-vocabulary scalar, and the grammar
/// reference that produced it from sample data alone. It cannot infer promotion,
/// canonicalization policy, or semantic mapping — those are curation decisions a human
/// (or a downstream corpus-coverage process, per Phase 4.3/4.4) makes once a suggestion
/// is reviewed. Generation is restricted to the closed KustoType/FieldAnnotationKind
/// vocabulary so suggested content is type-correct by construction.
/// </para>
/// </summary>
public static class ParserContractProjection
{
    /// <summary>
    /// Renders an existing catalog's inferrable subset in the Suggester's output shape —
    /// useful today as a fixture for what the Suggester will need to produce, and later as
    /// the format its real output is validated against.
    /// </summary>
    public static string Generate(SourceFieldCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var contract = new JsonObject {
            ["source"] = catalog.Source,
            ["suggestedFields"] = new JsonArray(catalog.Entries.Select(ToSuggestion).ToArray())
        };

        return contract.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonNode ToSuggestion(SourceFieldCatalogEntry entry)
    {
        var suggestion = new JsonObject {
            ["name"] = entry.Name,
            ["kqlScalar"] = entry.KustoType.ToString(),
            ["parserGrammarRef"] = entry.ParserGrammarRef
        };

        if (entry.Annotation is { } annotation)
        {
            suggestion["annotationKind"] = annotation.Kind.ToString();
        }

        return suggestion;
    }
}
