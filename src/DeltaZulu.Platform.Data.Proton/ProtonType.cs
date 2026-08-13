using DeltaZulu.Platform.Domain.Analytics.Schema;

namespace DeltaZulu.Platform.Data.Proton;

/// <summary>
/// Proton/ClickHouse type mapping keyed on <see cref="KustoType" />, the common contract every
/// backend derives from (see ADR 0007 and ADR 0014). Kept in-tree next to the emitter that
/// consumes it rather than a separate shared package.
/// </summary>
public static class KustoTypeExtensions
{
    /// <summary>Returns the Proton/ClickHouse column type string for the given Kusto type.</summary>
    public static string ToProtonSql(this KustoType type) => type switch {
        KustoType.String => "string",
        KustoType.Long => "int64",
        KustoType.Int => "int32",
        KustoType.Real => "float64",
        KustoType.Bool => "bool",
        KustoType.DateTime => "datetime64(3, 'UTC')",
        KustoType.Timespan => "int64",     // stored as microseconds; Proton has no native duration type
        KustoType.Dynamic => "string",     // Proton OSS has no documented native JSON type (ADR 0014)
        KustoType.Guid => "uuid",
        KustoType.Decimal => "float64",    // precision/scale not yet tracked on KustoType (ADR 0014 gap)
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown Kusto type")
    };

    /// <summary>
    /// Parses a Proton/ClickHouse column type string back into its Kusto type, for interpreting
    /// results read off Proton's native or HTTP query interface. Unwraps <c>nullable(...)</c> and
    /// strips type parameters such as <c>datetime64(3, 'UTC')</c> or <c>decimal(10, 2)</c>.
    /// </summary>
    public static (KustoType KustoType, bool Nullable) ToKustoType(this string protonType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protonType);

        var trimmed = protonType.Trim();
        var nullable = false;
        if (trimmed.StartsWith("nullable(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(')'))
        {
            nullable = true;
            trimmed = trimmed["nullable(".Length..^1].Trim();
        }

        var parenIndex = trimmed.IndexOf('(');
        var baseType = parenIndex < 0 ? trimmed : trimmed[..parenIndex];

        var kustoType = baseType.ToLowerInvariant() switch {
            "string" => KustoType.String,
            "int64" or "uint64" => KustoType.Long,
            "int32" or "uint32" or "int16" or "uint16" or "int8" or "uint8" => KustoType.Int,
            "float64" or "float32" => KustoType.Real,
            "bool" => KustoType.Bool,
            "datetime64" or "datetime" or "date32" or "date" => KustoType.DateTime,
            "uuid" => KustoType.Guid,
            "decimal" => KustoType.Decimal,
            "tuple" or "map" or "array" => KustoType.Dynamic,
            "ipv4" or "ipv6" => KustoType.String,
            _ => throw new ArgumentOutOfRangeException(nameof(protonType), protonType, "Unknown Proton type")
        };

        return (kustoType, nullable);
    }
}

/// <summary>
/// Legacy DuckDB-keyed Proton mapping, retained only for the mapping-expression DSL
/// (<c>CastExpr</c>/<c>TryCastExpr</c>) whose <c>TargetType</c> is still typed as
/// <see cref="DuckDbType" />. Schema/column DDL should prefer <see cref="KustoTypeExtensions" />.
/// </summary>
public static class ProtonTypeExtensions
{
    /// <summary>Returns the Proton/ClickHouse column type string for the given DuckDB type.</summary>
    public static string ToProtonSql(this DuckDbType type) => type switch {
        DuckDbType.Varchar => "string",
        DuckDbType.BigInt => "int64",
        DuckDbType.Integer => "int32",
        DuckDbType.Double => "float64",
        DuckDbType.Boolean => "bool",
        DuckDbType.Timestamp => "datetime64(3, 'UTC')",
        DuckDbType.Date => "date32",
        DuckDbType.Json => "string",
        DuckDbType.Blob => "string",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    /// <summary>Returns the full Proton column declaration type, wrapping nullable columns in <c>nullable(...)</c>.</summary>
    public static string ToProtonColumnType(this ColumnDef col)
    {
        ArgumentNullException.ThrowIfNull(col);
        var baseType = col.KustoType.ToProtonSql();
        return col.Nullable ? $"nullable({baseType})" : baseType;
    }
}