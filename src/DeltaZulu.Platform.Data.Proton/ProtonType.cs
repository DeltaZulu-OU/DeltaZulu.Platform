using DeltaZulu.Platform.Domain.Analytics.Schema;

namespace DeltaZulu.Platform.Data.Proton;

public static class ProtonTypeExtensions
{
    /// <summary>Returns the Proton/ClickHouse column type string for the given DuckDB type.</summary>
    public static string ToProtonSql(this DuckDbType type) => type switch {
        DuckDbType.Varchar   => "string",
        DuckDbType.BigInt    => "int64",
        DuckDbType.Integer   => "int32",
        DuckDbType.Double    => "float64",
        DuckDbType.Boolean   => "bool",
        DuckDbType.Timestamp => "datetime64(3, 'UTC')",
        DuckDbType.Date      => "date32",
        DuckDbType.Json      => "string",
        DuckDbType.Blob      => "string",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    /// <summary>Returns the full Proton column declaration type, wrapping nullable columns in <c>nullable(...)</c>.</summary>
    public static string ToProtonColumnType(this ColumnDef col)
    {
        ArgumentNullException.ThrowIfNull(col);
        var baseType = col.DuckDbType.ToProtonSql();
        return col.Nullable ? $"nullable({baseType})" : baseType;
    }
}
