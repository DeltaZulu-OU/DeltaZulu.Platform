namespace DeltaZulu.Platform.Domain.Analytics.Schema.Catalog;

/// <summary>
/// The closed set of translator-facing logical annotations. A <see cref="KustoType"/>
/// is the author-facing contract (what a KQL query sees); an annotation refines it
/// with physical/semantic detail the scalar alone cannot express — a unit, a decimal
/// precision, a bag-nested path, or a logical shape (IPv4/IPv6/MAC/GUID/bool) that a
/// parser grammar has not yet been given a first-class typed motif for.
/// </summary>
public enum FieldAnnotationKind
{
    /// <summary>Dotted-quad or equivalent textual IPv4 address.</summary>
    Ipv4,

    /// <summary>Textual IPv6 address.</summary>
    Ipv6,

    /// <summary>48-bit hardware/MAC address.</summary>
    Mac48,

    /// <summary>RFC 4122 UUID/GUID, extracted as text pending a first-class typed parser.</summary>
    Guid,

    /// <summary>Arbitrary-precision decimal value that would lose fidelity if stored as a double.</summary>
    Decimal,

    /// <summary>Boolean value extracted as text pending a first-class typed parser; see <see cref="FieldAnnotation.BoolLexemes"/>.</summary>
    Bool,

    /// <summary>Duration measured in <see cref="FieldAnnotation.Unit"/> before conversion to a canonical <see cref="KustoType.Timespan"/>.</summary>
    Duration,

    /// <summary>Field currently lives at a JSON path inside a <see cref="KustoType.Dynamic"/> bag; see <see cref="FieldAnnotation.Path"/>.</summary>
    NestedPath
}

/// <summary>
/// One translator-facing annotation on a catalog entry. Only the parameters relevant
/// to <see cref="Kind"/> are populated; the factory methods enforce that.
/// </summary>
public sealed record FieldAnnotation
{
    /// <summary>Boolean lexeme set, e.g. <c>["true","false"]</c> or <c>["T","F"]</c>. Set only for <see cref="FieldAnnotationKind.Bool"/>.</summary>
    public IReadOnlyList<string>? BoolLexemes { get; private init; }

    public FieldAnnotationKind Kind { get; private init; }

    /// <summary>JSON path within the <see cref="KustoType.Dynamic"/> bag this field currently lives at. Set only for <see cref="FieldAnnotationKind.NestedPath"/>.</summary>
    public string? Path { get; private init; }

    /// <summary>Decimal precision (total significant digits). Set only for <see cref="FieldAnnotationKind.Decimal"/>.</summary>
    public int? Precision { get; private init; }

    /// <summary>Decimal scale (digits after the decimal point). Set only for <see cref="FieldAnnotationKind.Decimal"/>.</summary>
    public int? Scale { get; private init; }

    /// <summary>Source unit of a duration value, e.g. <c>"ms"</c>, <c>"s"</c>, <c>"us"</c>. Set only for <see cref="FieldAnnotationKind.Duration"/>.</summary>
    public string? Unit { get; private init; }

    public static FieldAnnotation Simple(FieldAnnotationKind kind)
    {
        if (kind is FieldAnnotationKind.Bool or FieldAnnotationKind.Duration
            or FieldAnnotationKind.NestedPath or FieldAnnotationKind.Decimal)
        {
            throw new ArgumentException($"{kind} requires parameters; use the dedicated factory method.", nameof(kind));
        }

        return new FieldAnnotation { Kind = kind };
    }

    public static FieldAnnotation Bool(IReadOnlyList<string> lexemes)
    {
        ArgumentNullException.ThrowIfNull(lexemes);
        if (lexemes.Count == 0)
        {
            throw new ArgumentException("Bool annotation requires at least one lexeme.", nameof(lexemes));
        }

        return new FieldAnnotation { Kind = FieldAnnotationKind.Bool, BoolLexemes = lexemes };
    }

    public static FieldAnnotation Decimal(int precision, int scale)
    {
        if (precision <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(precision), precision, "Precision must be positive.");
        }

        if (scale < 0 || scale > precision)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "Scale must be between 0 and precision.");
        }

        return new FieldAnnotation { Kind = FieldAnnotationKind.Decimal, Precision = precision, Scale = scale };
    }

    public static FieldAnnotation Duration(string unit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        return new FieldAnnotation { Kind = FieldAnnotationKind.Duration, Unit = unit.Trim() };
    }

    public static FieldAnnotation NestedPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new FieldAnnotation { Kind = FieldAnnotationKind.NestedPath, Path = path.Trim() };
    }
}
