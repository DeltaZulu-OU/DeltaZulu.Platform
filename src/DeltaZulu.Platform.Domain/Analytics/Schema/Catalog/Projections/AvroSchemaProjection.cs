using System.Text.Json;
using System.Text.Json.Nodes;

namespace DeltaZulu.Platform.Domain.Analytics.Schema.Catalog.Projections;

/// <summary>
/// <para>
/// Projects a <see cref="SourceFieldCatalog"/> onto an Avro record schema (a valid <c>.avsc</c>
/// JSON document). Standard Avro logical types are used where one exists (uuid, decimal,
/// timestamp-micros); everything else uses a catalog-vocabulary logical type name that an
/// Avro reader without special handling will simply fall back to the base type for.
/// </para>
/// <para>
/// Every KQL scalar this catalog can emit (<see cref="KustoType"/> minus the forbidden
/// <see cref="KustoType.Int"/>) round-trips through Avro without data loss, though two are
/// not <em>structural</em> matches: <see cref="KustoType.Dynamic"/> has no fixed Avro schema
/// by definition, so it is carried as a JSON-text string rather than a native Avro record;
/// and <see cref="KustoType.Timespan"/> has no standard Avro logical type for an elapsed
/// duration (Avro's own <c>duration</c> logical type means calendar months/days/millis, and
/// <c>time-millis</c>/<c>time-micros</c> mean time-of-day), so it uses a catalog-defined one.
/// Both fall back to a plain <c>long</c>/<c>string</c> for a reader that does not recognize
/// the logical type — the value is preserved, only the interpretation hint is lost.
/// <see cref="KustoType.Decimal"/> is the one case that would otherwise silently lose fidelity
/// (falling back to a lossy <c>double</c>), so <see cref="SourceFieldCatalogEntry"/> refuses to
/// construct a Decimal-scalar entry without a Decimal annotation carrying explicit
/// precision/scale — every Decimal field this method sees already has one.
/// </para>
/// </summary>
public static class AvroSchemaProjection
{
    private const string AvroNamespace = "deltazulu.parse";

    public static string Generate(SourceFieldCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var record = new JsonObject {
            ["type"] = "record",
            ["name"] = ToPascalCase(catalog.Source),
            ["namespace"] = $"{AvroNamespace}.{catalog.Source}",
            ["fields"] = new JsonArray(catalog.Entries.Select(ToAvroField).ToArray())
        };

        return record.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonNode ToAvroField(SourceFieldCatalogEntry entry)
    {
        var type = ToAvroType(entry);
        return new JsonObject {
            ["name"] = entry.Name,
            ["type"] = new JsonArray("null", type), // every field is nullable: absent extraction is common on this port's per-message basis
            ["default"] = null
        };
    }

    private static JsonNode ToAvroType(SourceFieldCatalogEntry entry)
    {
        // Native KustoType->Avro mapping first; the annotation then overrides where it adds
        // fidelity the scalar alone cannot express.
        JsonNode baseType = entry.KustoType switch {
            KustoType.String => "string",
            KustoType.Long => "long",
            KustoType.Real => "double",
            KustoType.Bool => "boolean",
            KustoType.DateTime => new JsonObject { ["type"] = "long", ["logicalType"] = "timestamp-micros" },
            KustoType.Timespan => new JsonObject { ["type"] = "long", ["logicalType"] = "timespan-micros" },
            KustoType.Dynamic => new JsonObject { ["type"] = "string", ["logicalType"] = "json" },
            KustoType.Guid => new JsonObject { ["type"] = "string", ["logicalType"] = "uuid" },
            // Unreachable for a valid entry: SourceFieldCatalogEntry requires a Decimal
            // annotation (with precision/scale) whenever KustoType is Decimal, which the
            // switch below always overrides to a real Avro `decimal` logical type before
            // this value would be returned.
            KustoType.Decimal => "double",
            _ => throw new ArgumentOutOfRangeException(nameof(entry), entry.KustoType, "Unknown Kusto type"),
        };

        if (entry.Annotation is not { } annotation)
        {
            return baseType;
        }

        return annotation.Kind switch {
            FieldAnnotationKind.Ipv4 => new JsonObject { ["type"] = "string", ["logicalType"] = "ipv4" },
            FieldAnnotationKind.Ipv6 => new JsonObject { ["type"] = "string", ["logicalType"] = "ipv6" },
            FieldAnnotationKind.Mac48 => new JsonObject { ["type"] = "string", ["logicalType"] = "mac48" },
            FieldAnnotationKind.Guid => new JsonObject { ["type"] = "string", ["logicalType"] = "uuid" },
            FieldAnnotationKind.Decimal => new JsonObject {
                ["type"] = "bytes",
                ["logicalType"] = "decimal",
                ["precision"] = annotation.Precision,
                ["scale"] = annotation.Scale
            },
            FieldAnnotationKind.Bool => new JsonObject {
                ["type"] = "string",
                ["logicalType"] = "bool-lexeme",
                ["lexemes"] = new JsonArray(annotation.BoolLexemes!.Select(l => (JsonNode)l).ToArray())
            },
            // Duration only ever annotates a raw numeric field (pending conversion to a canonical
            // Timespan); compute the primitive directly rather than unwrap baseType generically.
            FieldAnnotationKind.Duration => new JsonObject {
                ["type"] = entry.KustoType == KustoType.Real ? "double" : "long",
                ["logicalType"] = $"duration-{annotation.Unit}"
            },
            FieldAnnotationKind.NestedPath => baseType, // documentation-only; does not change the wire type
            _ => baseType
        };
    }

    private static string ToPascalCase(string source)
        => string.Concat(source.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
