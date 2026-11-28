using DuckDB.NET.Native;
using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DeltaZulu.Platform.Data.DuckDb;

/// <summary>
/// Normalizes dynamic DuckDB result values before they leave the data layer.
/// Dapper is used for fixed application persistence. Dynamic KQL output still
/// needs a DuckDB-aware reader because extension and nested types are provider-specific.
/// </summary>
public static class DuckDbValueReader
{
    public static object? ReadValue(DbDataReader reader, int ordinal)
        => ReadValue(reader, ordinal, logicalTypeName: null);

    public static object? ReadValue(DbDataReader reader, int ordinal, string? logicalTypeName)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        // DuckDB.NET exposes the native type ID as an enum name, not a SQL type declaration.
        var typeName = reader.GetDataTypeName(ordinal);
        if (!Enum.TryParse<DuckDBType>(typeName, out var type) || type.ToString() != typeName)
        {
            throw new NotSupportedException($"Unrecognised DuckDB result type '{typeName}'.");
        }

        var presentationType = GetPresentationType(logicalTypeName);
        if (presentationType is not null)
        {
            if (type != DuckDBType.Varchar)
            {
                throw new InvalidCastException("DuckDB result must be converted to text by the native result projection.");
            }

            var text = reader.GetString(ordinal);
            if (presentationType != "INET")
            {
                // DuckDB permits NaN and Infinity in JSON; the presentation contract does not.
                using var json = JsonDocument.Parse(text);
            }

            return text;
        }

        return type switch
        {
            DuckDBType.Decimal => ReadExactDecimal(reader.GetFieldValue<DuckDBDecimal>(ordinal)),
            DuckDBType.Interval => (TimeSpan)reader.GetFieldValue<DuckDBInterval>(ordinal),
            DuckDBType.Uuid => reader.GetGuid(ordinal),
            DuckDBType.TimestampTz => reader.GetFieldValue<DateTimeOffset>(ordinal).UtcDateTime,
            DuckDBType.Struct or DuckDBType.List or DuckDBType.Array or DuckDBType.Map => throw new NotSupportedException("Nested DuckDB values require native result projection before materialization."),
            _ => ReadNonNullValue(reader, ordinal)
        };
    }

    private static object ReadNonNullValue(DbDataReader reader, int ordinal)
    {
        var value = reader.GetValue(ordinal);
        return value is null or DBNull
            ? throw new InvalidCastException($"Non-null DuckDB column '{reader.GetName(ordinal)}' returned no value.")
            : value;
    }

    private static decimal ReadExactDecimal(DuckDBDecimal value)
    {
        var mantissa = value.Value.ToInt128();
        var scale = value.Scale;
        while (scale > 0 && mantissa % 10 == 0)
        {
            mantissa /= 10;
            scale--;
        }

        var magnitude = (UInt128)(mantissa < 0 ? -mantissa : mantissa);
        if (scale > 28 || magnitude > (((UInt128)1 << 96) - 1))
        {
            throw new OverflowException("DuckDB decimal cannot be represented exactly as System.Decimal.");
        }

        return new decimal(unchecked((int)magnitude), unchecked((int)(magnitude >> 32)),
            unchecked((int)(magnitude >> 64)), mantissa < 0, scale);
    }

    // DESCRIBE returns SQL type declarations, unlike the provider's physical IDs.
    internal static string? GetPresentationType(string? logicalTypeName)
    {
        if (logicalTypeName is null) return null;
        if (logicalTypeName == "INET") return "INET";
        if (logicalTypeName.EndsWith("[]", StringComparison.Ordinal)) return "List";
        if (Regex.IsMatch(logicalTypeName, @"\[\d+\]$")) return "Array";
        if (logicalTypeName.StartsWith("STRUCT(", StringComparison.Ordinal)) return "Struct";
        if (logicalTypeName.StartsWith("MAP(", StringComparison.Ordinal)) return "Map";
        return null;
    }

    internal static void ValidateNestedPresentationType(string logicalTypeName)
    {
        // Conservative checks: JSON would expose INET internals or stringify non-string map keys.
        // Reject these declarations rather than invent a recursive conversion format.
        if (Regex.IsMatch(logicalTypeName, @"\bINET\b|\bUNION\s*\(|\bMAP\s*\((?!\s*VARCHAR\s*,)", RegexOptions.IgnoreCase))
        {
            throw new NotSupportedException("Nested INET, UNION and non-string-keyed MAP values are not supported by JSON result presentation.");
        }
    }
}
