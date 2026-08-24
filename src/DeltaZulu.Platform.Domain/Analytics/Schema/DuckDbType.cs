namespace DeltaZulu.Platform.Domain.Analytics.Schema;

/// <summary>
/// DuckDB column types used in schema generation and SQL emission.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Members mirror DuckDB's native type names")]
public enum DuckDbType
{
    Varchar,
    BigInt,
    Integer,
    Double,
    Decimal,
    Boolean,
    Timestamp,
    Date,
    Json,
    Blob,

    // Appended rather than inserted so existing ordinals stay stable.
    // DuckDB's native UUID is core; INET comes from the `inet` extension, which
    // DuckDbConnectionFactory installs and loads on every connection.
    Uuid,
    Inet
}

public static class DuckDbTypeExtensions
{
    // Exhaustive over DuckDbType with no `_ =>` default. The default arm was removed
    // when Uuid and Inet were added: a fallthrough over a closed estate enum defers to
    // runtime what exhaustive listing catches at compile time (CS8509), and adding a
    // member is exactly when that distinction costs something.
    public static string ToSql(this DuckDbType type) => type switch {
        DuckDbType.Varchar => "VARCHAR",
        DuckDbType.BigInt => "BIGINT",
        DuckDbType.Integer => "INTEGER",
        DuckDbType.Double => "DOUBLE",
        DuckDbType.Decimal => "DECIMAL",
        DuckDbType.Boolean => "BOOLEAN",
        DuckDbType.Timestamp => "TIMESTAMP",
        DuckDbType.Date => "DATE",
        DuckDbType.Json => "JSON",
        DuckDbType.Blob => "BLOB",
        DuckDbType.Uuid => "UUID",
        DuckDbType.Inet => "INET",
    };
}
