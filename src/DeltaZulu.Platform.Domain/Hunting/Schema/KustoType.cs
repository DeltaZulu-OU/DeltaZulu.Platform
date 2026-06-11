namespace DeltaZulu.Platform.Domain.Hunting.Schema;

/// <summary>
/// Kusto scalar types as exposed to KQL users and Monaco editor.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Members mirror Kusto's native type names")]
public enum KustoType
{
    String,
    Long,
    Int,
    Real,
    Bool,
    DateTime,
    Timespan,
    Dynamic,
    Guid,
    Decimal
}

public static class KustoTypeExtensions
{
    /// <summary>
    /// Returns the Kusto type name as used in schema declarations (e.g. "(col: string)").
    /// </summary>
    public static string ToKustoName(this KustoType type) => type switch
    {
        KustoType.String => "string",
        KustoType.Long => "long",
        KustoType.Int => "int",
        KustoType.Real => "real",
        KustoType.Bool => "bool",
        KustoType.DateTime => "datetime",
        KustoType.Timespan => "timespan",
        KustoType.Dynamic => "dynamic",
        KustoType.Guid => "guid",
        KustoType.Decimal => "decimal",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown Kusto type")
    };

    /// <summary>
    /// Default DuckDB type mapping for a Kusto type.
    /// </summary>
    public static DuckDbType ToDefaultDuckDbType(this KustoType type) => type switch
    {
        KustoType.String => DuckDbType.Varchar,
        KustoType.Long => DuckDbType.BigInt,
        KustoType.Int => DuckDbType.Integer,
        KustoType.Real => DuckDbType.Double,
        KustoType.Bool => DuckDbType.Boolean,
        KustoType.DateTime => DuckDbType.Timestamp,
        KustoType.Timespan => DuckDbType.BigInt,      // stored as microseconds
        KustoType.Dynamic => DuckDbType.Json,
        KustoType.Guid => DuckDbType.Varchar,
        KustoType.Decimal => DuckDbType.Double,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown Kusto type")
    };
}