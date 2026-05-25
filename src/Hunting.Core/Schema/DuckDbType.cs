namespace Hunting.Core.Schema;

/// <summary>
/// DuckDB column types used in schema generation and SQL emission.
/// </summary>
public enum DuckDbType
{
    Varchar,
    BigInt,
    Integer,
    Double,
    Boolean,
    Timestamp,
    Date,
    Json,
    Blob
}

public static class DuckDbTypeExtensions
{
    public static string ToSql(this DuckDbType type) => type switch
    {
        DuckDbType.Varchar => "VARCHAR",
        DuckDbType.BigInt => "BIGINT",
        DuckDbType.Integer => "INTEGER",
        DuckDbType.Double => "DOUBLE",
        DuckDbType.Boolean => "BOOLEAN",
        DuckDbType.Timestamp => "TIMESTAMP",
        DuckDbType.Date => "DATE",
        DuckDbType.Json => "JSON",
        DuckDbType.Blob => "BLOB",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown DuckDB type")
    };
}