using DuckDB.NET.Native;
using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Numerics;
using System.Text.Json;

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

        if (string.Equals(logicalTypeName, "INET", StringComparison.OrdinalIgnoreCase))
        {
            if (type != DuckDBType.Struct)
            {
                throw new InvalidCastException("INET metadata does not match the native DuckDB result type.");
            }

            return ReadInet(reader.GetFieldValue<Dictionary<string, object?>>(ordinal));
        }

        return type switch
        {
            DuckDBType.Decimal => ReadExactDecimal(reader.GetFieldValue<DuckDBDecimal>(ordinal)),
            DuckDBType.Interval => (TimeSpan)reader.GetFieldValue<DuckDBInterval>(ordinal),
            DuckDBType.Uuid => reader.GetGuid(ordinal),
            DuckDBType.TimestampTz => reader.GetFieldValue<DateTimeOffset>(ordinal).UtcDateTime,
            DuckDBType.Struct or DuckDBType.List or DuckDBType.Array or DuckDBType.Map => ReadNestedValueAsString(reader, ordinal),
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

    // Temporary presentation compatibility. Nested fidelity requires a separate typed read path.
    private static string ReadNestedValueAsString(DbDataReader reader, int ordinal)
        => JsonSerializer.Serialize(ReadNonNullValue(reader, ordinal));

    // INET's physical representation is an extension implementation detail. Only
    // decode it when logical metadata identifies INET, never by matching field names.
    private static string ReadInet(Dictionary<string, object?> fields)
    {
        if (fields.Count != 3
            || !fields.TryGetValue("ip_type", out var familyValue) || familyValue is not byte family
            || !fields.TryGetValue("address", out var addressValue) || addressValue is not BigInteger address
            || !fields.TryGetValue("mask", out var maskValue) || maskValue is not ushort mask)
        {
            throw new InvalidCastException("Unexpected DuckDB INET physical representation.");
        }

        if (family == 1 && mask <= 32 && address >= 0 && address <= uint.MaxValue)
        {
            var raw = (uint)address;
            var ip = new IPAddress(new byte[] { (byte)(raw >> 24), (byte)(raw >> 16), (byte)(raw >> 8), (byte)raw });
            return mask == 32 ? ip.ToString() : ip + "/" + mask.ToString(CultureInfo.InvariantCulture);
        }

        // DuckDB INET flips the high bit of IPv6 addresses to preserve unsigned
        // ordering in its signed HUGEINT field. Adding 2^127 reverses that bias.
        var bias = BigInteger.One << 127;
        if (family == 2 && mask <= 128 && address >= -bias && address < bias)
        {
            var unsignedAddress = address + bias;
            var bytes = unsignedAddress.ToByteArray(isUnsigned: true, isBigEndian: true);
            var padded = new byte[16];
            bytes.CopyTo(padded, padded.Length - bytes.Length);
            var ip = new IPAddress(padded);
            return mask == 128 ? ip.ToString() : ip + "/" + mask.ToString(CultureInfo.InvariantCulture);
        }

        throw new InvalidCastException("DuckDB INET address family, address or prefix is out of range.");
    }
}
