using System.Text.RegularExpressions;

namespace DeltaZulu.Platform.Domain.Analytics.Schema;

public enum ParserCanonicalization { None, Utc, MacLowerColon, Ipv6Compressed }
public enum ParserFieldPlacement { TopLevel, DynamicBag }
public sealed record BooleanLexemePair(string False, string True);

/// <summary>Parser provenance attached to the existing logical registry rather than a second catalog.</summary>
public sealed record ParserFieldContract(
    string GrammarReference,
    ParserFieldPlacement Placement,
    string? DynamicBagPath = null,
    ParserCanonicalization Canonicalization = ParserCanonicalization.None,
    BooleanLexemePair? BooleanLexemes = null);

/// <summary>Validates versioned registry schemas before backend projection.</summary>
public static partial class LogicalSchemaValidator
{
    public static void Validate(LogicalSchemaVersion schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema.ProducerFamily);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema.SchemaName);
        ArgumentOutOfRangeException.ThrowIfLessThan(schema.Version, 1);
        if (schema.Fields.Count == 0) throw new ArgumentException("A logical schema must contain at least one field.", nameof(schema));

        var duplicate = schema.Fields.GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new ArgumentException($"Schema contains duplicate field '{duplicate.Key}'.", nameof(schema));
        foreach (var field in schema.Fields) ValidateField(field);
    }

    private static void ValidateField(LogicalFieldDef field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field.Name);
        if (!FieldNamePattern().IsMatch(field.Name)) throw new ArgumentException($"Invalid logical field name '{field.Name}'.", nameof(field));
        var type = field.Type;
        if (type.Family == LogicalFieldFamily.Decimal && (type.DecimalPrecision is null or <= 0 || type.DecimalScale is null or < 0 || type.DecimalScale > type.DecimalPrecision))
            throw new ArgumentException($"Decimal field '{field.Name}' requires a valid precision and scale.", nameof(field));
        foreach (var target in Enum.GetValues<RegistryProjectionTarget>())
            if (type.BackendMappings.Count(m => m.Target == target) != 1)
                throw new ArgumentException($"Field '{field.Name}' must have exactly one {target} mapping.", nameof(field));

        if (field.Parser is not { } parser) return;
        ArgumentException.ThrowIfNullOrWhiteSpace(parser.GrammarReference);
        if (parser.Placement == ParserFieldPlacement.DynamicBag && string.IsNullOrWhiteSpace(parser.DynamicBagPath))
            throw new ArgumentException($"Dynamic-bag field '{field.Name}' requires a bag path.", nameof(field));
        if (parser.Placement == ParserFieldPlacement.TopLevel && parser.DynamicBagPath is not null)
            throw new ArgumentException($"Top-level field '{field.Name}' cannot declare a dynamic-bag path.", nameof(field));
        if (parser.BooleanLexemes is not null && type.Family != LogicalFieldFamily.Boolean)
            throw new ArgumentException($"Boolean lexemes on '{field.Name}' require a Boolean logical type.", nameof(field));
        if (parser.Canonicalization == ParserCanonicalization.Utc && type.Family != LogicalFieldFamily.Timestamp)
            throw new ArgumentException($"UTC canonicalization on '{field.Name}' requires a Timestamp logical type.", nameof(field));
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex FieldNamePattern();
}

/// <summary>Projects approved backend contracts from the versioned logical registry.</summary>
public static class LogicalSchemaProjection
{
    public static InternalTableDef ToSilverTable(LogicalSchemaVersion schema)
    {
        LogicalSchemaValidator.Validate(schema);
        var columns = schema.Fields.Where(f => f.Parser?.Placement == ParserFieldPlacement.TopLevel).Select(ToColumn).ToArray();
        return new InternalTableDef("silver", $"{schema.ProducerFamily}_{schema.SchemaName}_v{schema.Version}", columns, $"Registry projection of {schema.RegistryKey}.");
    }

    public static AgentSinkSchema ToAgentSink(LogicalSchemaVersion schema, AgentOutputSink sink)
    {
        LogicalSchemaValidator.Validate(schema);
        var target = sink switch { AgentOutputSink.Quack => RegistryProjectionTarget.DuckDb, AgentOutputSink.Proton => RegistryProjectionTarget.Proton, _ => throw new ArgumentOutOfRangeException(nameof(sink), sink, null) };
        var fields = schema.Fields.Select(f => new AgentSinkField(f.Name, Mapping(f.Type, target).TypeName, f.Type.Nullable, f.Parser?.Placement, f.Parser?.DynamicBagPath)).ToArray();
        return new AgentSinkSchema(schema.RegistryKey, sink, fields);
    }

    private static ColumnDef ToColumn(LogicalFieldDef field)
    {
        var duck = Mapping(field.Type, RegistryProjectionTarget.DuckDb);
        var proton = Mapping(field.Type, RegistryProjectionTarget.Proton);
        return new ColumnDef(field.Name, ToDuckDbType(field.Type.Family), ToKustoType(field.Type.Family), field.Type.Nullable, field.Description, duck.TypeName, proton.TypeName, field.Type);
    }

    /// <summary>
    /// Resolves the backend mapping a logical type declares for one projection target.
    /// </summary>
    /// <remarks>
    /// This used <c>Single(...)</c>, which reports a missing mapping as
    /// <i>"Sequence contains no matching element"</i> — a message that names neither
    /// the type nor the target, so the actual fault has to be rediscovered every time.
    /// </remarks>
    private static LogicalFieldBackendMapping Mapping(LogicalFieldType type, RegistryProjectionTarget target)
    {
        var matches = type.BackendMappings.Where(m => m.Target == target).ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException(
                $"Logical family '{type.Family}' declares no backend mapping for target " +
                $"'{target}'. Declared targets: " +
                $"{string.Join(", ", type.BackendMappings.Select(m => m.Target))}."),
            _ => throw new InvalidOperationException(
                $"Logical family '{type.Family}' declares {matches.Length} backend mappings " +
                $"for target '{target}'; exactly one is required."),
        };
    }

    // The arms below are exhaustive over LogicalFieldFamily, with no `_ =>` default.
    // A closed estate enum must not have a fallthrough: with one, adding a member is a
    // runtime surprise on whichever record first uses it; without one, the compiler
    // reports the gap (CS8509) at the point the member is added. Binary, Array and Map
    // are listed as explicit rejections rather than mapped, because choosing their
    // physical types is a contract decision and inventing one here would be worse than
    // refusing.

    private static DuckDbType ToDuckDbType(LogicalFieldFamily family) => family switch {
        LogicalFieldFamily.Boolean => DuckDbType.Boolean,
        LogicalFieldFamily.Integer or LogicalFieldFamily.Duration => DuckDbType.BigInt,
        LogicalFieldFamily.FloatingPoint => DuckDbType.Double,
        LogicalFieldFamily.Timestamp => DuckDbType.Timestamp,
        LogicalFieldFamily.Dynamic or LogicalFieldFamily.Nested => DuckDbType.Json,
        LogicalFieldFamily.Decimal => DuckDbType.Decimal,
        LogicalFieldFamily.String or LogicalFieldFamily.Uuid or LogicalFieldFamily.IpAddress => DuckDbType.Varchar,
        LogicalFieldFamily.Binary or LogicalFieldFamily.Array or LogicalFieldFamily.Map =>
            throw new NotSupportedException(
                $"Logical family '{family}' has no declared DuckDB representation. " +
                "Choosing one is a contract decision, not a default."),
    };

    private static KustoType ToKustoType(LogicalFieldFamily family) => family switch {
        LogicalFieldFamily.String or LogicalFieldFamily.IpAddress => KustoType.String,
        LogicalFieldFamily.Boolean => KustoType.Bool,
        LogicalFieldFamily.Integer => KustoType.Long,
        LogicalFieldFamily.FloatingPoint => KustoType.Real,
        LogicalFieldFamily.Decimal => KustoType.Decimal,
        LogicalFieldFamily.Timestamp => KustoType.DateTime,
        LogicalFieldFamily.Duration => KustoType.Timespan,
        LogicalFieldFamily.Uuid => KustoType.Guid,
        LogicalFieldFamily.Dynamic or LogicalFieldFamily.Nested => KustoType.Dynamic,
        LogicalFieldFamily.Binary or LogicalFieldFamily.Array or LogicalFieldFamily.Map =>
            throw new NotSupportedException(
                $"Logical family '{family}' has no KQL scalar mapping. Coercing it to " +
                "string would be the silent type loss the wire contract forbids."),
    };
}

public enum AgentOutputSink { Quack, Proton }
public sealed record AgentSinkField(string Name, string PhysicalType, bool Nullable, ParserFieldPlacement? Placement, string? DynamicBagPath);
public sealed record AgentSinkSchema(string RegistryKey, AgentOutputSink Sink, IReadOnlyList<AgentSinkField> Fields);
