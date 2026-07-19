using System.Text.Json;
using System.Text.Json.Nodes;

namespace DeltaZulu.Platform.Domain.Analytics.Schema.Catalog;

/// <summary>
/// The type contract catalog for one log source: the single, machine-readable source
/// of truth every projection (Avro schema, Arrow schema, Proton DDL, DuckDB DDL, and the
/// parser-suggestion contract) generates from. See ADR-2.
/// </summary>
public sealed class SourceFieldCatalog
{
    public IReadOnlyList<SourceFieldCatalogEntry> Entries { get; }

    /// <summary>Logical source family name (e.g. <c>cef_firewall</c>), matching Bronze's <c>source_name</c> convention.</summary>
    public string Source { get; }

    public SourceFieldCatalog(string source, IReadOnlyList<SourceFieldCatalogEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            throw new ArgumentException("A catalog must describe at least one field.", nameof(entries));
        }

        var duplicate = entries
            .GroupBy(static e => e.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static g => g.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException($"Catalog contains duplicate field '{duplicate.Key}'.", nameof(entries));
        }

        Source = source.Trim();
        Entries = entries;
    }

    public static SourceFieldCatalog LoadFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var root = JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidOperationException("Catalog JSON must be an object.");

        var source = (string?)root["source"]
            ?? throw new InvalidOperationException("Catalog JSON must have a 'source' field.");

        var fields = root["fields"]?.AsArray()
            ?? throw new InvalidOperationException("Catalog JSON must have a 'fields' array.");

        var entries = fields.Select(ReadEntry).ToList();
        return new SourceFieldCatalog(source, entries);
    }

    public string ToJson()
    {
        var root = new JsonObject {
            ["source"] = Source,
            ["fields"] = new JsonArray(Entries.Select(WriteEntry).ToArray())
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static SourceFieldCatalogEntry ReadEntry(JsonNode? node)
    {
        var obj = node?.AsObject() ?? throw new InvalidOperationException("Each catalog field must be an object.");

        var name = (string?)obj["name"] ?? throw new InvalidOperationException("Field entry missing 'name'.");
        var kustoType = Enum.Parse<KustoType>((string?)obj["kqlScalar"]
            ?? throw new InvalidOperationException($"Field '{name}' missing 'kqlScalar'."));
        var parserGrammarRef = (string?)obj["parserGrammarRef"]
            ?? throw new InvalidOperationException($"Field '{name}' missing 'parserGrammarRef'.");
        var canonicalization = obj["canonicalization"] is JsonValue c
            ? Enum.Parse<CanonicalizationPolicy>((string)c!)
            : CanonicalizationPolicy.None;
        var promoted = (bool?)obj["promoted"] ?? false;
        var semantic = (string?)obj["semantic"];
        var annotation = obj["annotation"] is JsonObject annotationObj ? ReadAnnotation(annotationObj) : null;

        return new SourceFieldCatalogEntry(name, kustoType, parserGrammarRef, annotation, canonicalization, promoted, semantic);
    }

    private static FieldAnnotation ReadAnnotation(JsonObject obj)
    {
        var kind = Enum.Parse<FieldAnnotationKind>((string?)obj["kind"]
            ?? throw new InvalidOperationException("Annotation missing 'kind'."));

        return kind switch {
            FieldAnnotationKind.Bool => FieldAnnotation.Bool(
                obj["lexemes"]?.AsArray().Select(v => (string)v!).ToList()
                    ?? throw new InvalidOperationException("Bool annotation missing 'lexemes'.")),
            FieldAnnotationKind.Decimal => FieldAnnotation.Decimal(
                (int?)obj["precision"] ?? throw new InvalidOperationException("Decimal annotation missing 'precision'."),
                (int?)obj["scale"] ?? throw new InvalidOperationException("Decimal annotation missing 'scale'.")),
            FieldAnnotationKind.Duration => FieldAnnotation.Duration(
                (string?)obj["unit"] ?? throw new InvalidOperationException("Duration annotation missing 'unit'.")),
            FieldAnnotationKind.NestedPath => FieldAnnotation.NestedPath(
                (string?)obj["path"] ?? throw new InvalidOperationException("NestedPath annotation missing 'path'.")),
            _ => FieldAnnotation.Simple(kind)
        };
    }

    private static JsonObject WriteEntry(SourceFieldCatalogEntry entry)
        => new() {
            ["name"] = entry.Name,
            ["kqlScalar"] = entry.KustoType.ToString(),
            ["annotation"] = entry.Annotation is { } a ? WriteAnnotation(a) : null,
            ["parserGrammarRef"] = entry.ParserGrammarRef,
            ["canonicalization"] = entry.Canonicalization.ToString(),
            ["promoted"] = entry.Promoted,
            ["semantic"] = entry.Semantic
        };

    private static JsonObject WriteAnnotation(FieldAnnotation annotation)
    {
        var obj = new JsonObject { ["kind"] = annotation.Kind.ToString() };
        switch (annotation.Kind)
        {
            case FieldAnnotationKind.Bool:
                obj["lexemes"] = new JsonArray(annotation.BoolLexemes!.Select(l => (JsonNode)l).ToArray());
                break;
            case FieldAnnotationKind.Decimal:
                obj["precision"] = annotation.Precision;
                obj["scale"] = annotation.Scale;
                break;
            case FieldAnnotationKind.Duration:
                obj["unit"] = annotation.Unit;
                break;
            case FieldAnnotationKind.NestedPath:
                obj["path"] = annotation.Path;
                break;
        }

        return obj;
    }
}
