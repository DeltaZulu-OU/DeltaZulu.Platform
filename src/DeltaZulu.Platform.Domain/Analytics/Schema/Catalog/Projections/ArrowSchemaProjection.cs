using System.Text.Json;
using System.Text.Json.Nodes;

namespace DeltaZulu.Platform.Domain.Analytics.Schema.Catalog.Projections;

/// <summary>
/// <para>
/// Projects a <see cref="SourceFieldCatalog"/> onto an Arrow schema description. This is a
/// JSON rendering of what an Arrow <c>Schema</c>/<c>Field</c> pair would carry — type,
/// nullability, and metadata — not a call into the Apache.Arrow library, which is not yet a
/// dependency of this repo (see ADR-2). Where Arrow has a native type Avro lacks (Duration,
/// Decimal128) it is used directly instead of a logical-type annotation.
/// </para>
/// <para>
/// Metadata keys follow Arrow's own extension-type convention (<c>ARROW:extension:*</c>) so
/// adopting the real Apache.Arrow builder later is a mechanical, not a semantic, change.
/// </para>
/// <para>
/// Arrow covers every KQL scalar this catalog can emit with less approximation than Avro:
/// <see cref="KustoType.Timespan"/> and <see cref="KustoType.Decimal"/> (once annotated with
/// precision/scale — see <see cref="AvroSchemaProjection"/>'s remarks) both have true native
/// Arrow types, not logical-type overlays. Only <see cref="KustoType.Dynamic"/> remains a
/// structural approximation (JSON text, not a native Arrow struct/list), for the same reason
/// it is in Avro: its shape is unknown at schema-authoring time by definition.
/// </para>
/// </summary>
public static class ArrowSchemaProjection
{
    public static string Generate(SourceFieldCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var schema = new JsonObject {
            ["name"] = catalog.Source,
            ["fields"] = new JsonArray(catalog.Entries.Select(ToArrowField).ToArray())
        };

        return schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonNode ToArrowField(SourceFieldCatalogEntry entry)
    {
        var (arrowType, metadata) = ToArrowType(entry);

        var field = new JsonObject {
            ["name"] = entry.Name,
            ["type"] = arrowType,
            ["nullable"] = true
        };

        if (metadata.Count > 0)
        {
            field["metadata"] = new JsonObject(metadata.Select(kv => new KeyValuePair<string, JsonNode?>(kv.Key, kv.Value)));
        }

        return field;
    }

    private static (string ArrowType, Dictionary<string, JsonNode?> Metadata) ToArrowType(SourceFieldCatalogEntry entry)
    {
        var metadata = new Dictionary<string, JsonNode?>();

        var baseType = entry.KustoType switch {
            KustoType.String => "utf8",
            KustoType.Long => "int64",
            KustoType.Real => "float64",
            KustoType.Bool => "bool",
            KustoType.DateTime => "timestamp[us, tz=UTC]",
            KustoType.Timespan => "duration[us]", // Arrow has a native Duration type; no annotation needed
            KustoType.Dynamic => "utf8",
            KustoType.Guid => "utf8",
            // Unreachable for a valid entry: SourceFieldCatalogEntry requires a Decimal
            // annotation whenever KustoType is Decimal, which the switch below always
            // overrides to a native decimal128(precision, scale) before this is returned.
            KustoType.Decimal => "float64",
            _ => throw new ArgumentOutOfRangeException(nameof(entry), entry.KustoType, "Unknown Kusto type"),
        };

        if (entry.KustoType == KustoType.Dynamic)
        {
            metadata["ARROW:extension:name"] = "deltazulu.json";
        }

        if (entry.Annotation is not { } annotation)
        {
            return (baseType, metadata);
        }

        switch (annotation.Kind)
        {
            case FieldAnnotationKind.Ipv4:
                metadata["ARROW:extension:name"] = "deltazulu.ipv4";
                break;
            case FieldAnnotationKind.Ipv6:
                metadata["ARROW:extension:name"] = "deltazulu.ipv6";
                break;
            case FieldAnnotationKind.Mac48:
                metadata["ARROW:extension:name"] = "deltazulu.mac48";
                break;
            case FieldAnnotationKind.Guid:
                metadata["ARROW:extension:name"] = "deltazulu.uuid";
                break;
            case FieldAnnotationKind.Decimal:
                // Arrow has a native decimal128(precision, scale); use it directly.
                baseType = $"decimal128({annotation.Precision}, {annotation.Scale})";
                break;
            case FieldAnnotationKind.Bool:
                metadata["ARROW:extension:name"] = "deltazulu.bool_lexeme";
                metadata["deltazulu:lexemes"] = string.Join(",", annotation.BoolLexemes!);
                break;
            case FieldAnnotationKind.Duration:
                baseType = $"duration[{annotation.Unit}]";
                break;
            case FieldAnnotationKind.NestedPath:
                metadata["deltazulu:source_path"] = annotation.Path; // documentation-only
                break;
        }

        return (baseType, metadata);
    }
}
