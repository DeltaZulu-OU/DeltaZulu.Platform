using DuckDB.NET.Native;
using System.Collections;
using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Numerics;
using System.Reflection;
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

        return type switch
        {
            DuckDBType.Decimal => ReadExactDecimal(reader.GetFieldValue<DuckDBDecimal>(ordinal)),
            DuckDBType.Interval => (TimeSpan)reader.GetFieldValue<DuckDBInterval>(ordinal),
            DuckDBType.Uuid => reader.GetGuid(ordinal),
            DuckDBType.TimestampTz => reader.GetFieldValue<DateTimeOffset>(ordinal).UtcDateTime,
            DuckDBType.Struct => ReadNestedValueAsString(reader, ordinal, allowLegacyInet: true),
            DuckDBType.List or DuckDBType.Array or DuckDBType.Map => ReadNestedValueAsString(reader, ordinal, allowLegacyInet: false),
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
    private static string ReadNestedValueAsString(DbDataReader reader, int ordinal, bool allowLegacyInet)
    {
        var value = ReadNonNullValue(reader, ordinal);
        if (allowLegacyInet && TryFormatInet(value, out var formattedInet))
        {
            return formattedInet;
        }

        // Materialization and serialization errors must propagate, never become text or SQL NULL.
        return JsonSerializer.Serialize(value);
    }

    private static bool TryFormatInet(object value, out string formatted)
    {
        formatted = string.Empty;

        if (!TryGetMemberValue(value, "ip_type", out var ipTypeValue)
            && !TryGetMemberValue(value, "IpType", out ipTypeValue))
        {
            return false;
        }

        if (!TryGetMemberValue(value, "address", out var addressValue)
            && !TryGetMemberValue(value, "Address", out addressValue))
        {
            return false;
        }

        if (!TryGetMemberValue(value, "mask", out var maskValue))
        {
            _ = TryGetMemberValue(value, "Mask", out maskValue);
        }

        if (!TryToInt32(ipTypeValue, out var ipType)
            || !TryToBigInteger(addressValue, out var address))
        {
            return false;
        }

        var hasMask = TryToInt32(maskValue, out var mask);

        if (ipType == 1)
        {
            var raw = (uint)address;
            formatted = $"{(raw >> 24) & 0xff}.{(raw >> 16) & 0xff}.{(raw >> 8) & 0xff}.{raw & 0xff}";
            if (hasMask && mask != 32)
            {
                formatted += $"/{mask}";
            }

            return true;
        }

        if (ipType == 2)
        {
            var bytes = address.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (bytes.Length < 16)
            {
                var padded = new byte[16];
                Buffer.BlockCopy(bytes, 0, padded, 16 - bytes.Length, bytes.Length);
                bytes = padded;
            }
            else if (bytes.Length > 16)
            {
                bytes = bytes[^16..];
            }

            formatted = new IPAddress(bytes).ToString();
            if (hasMask && mask != 128)
            {
                formatted += $"/{mask}";
            }

            return true;
        }

        return false;
    }

    private static bool TryGetMemberValue(object value, string memberName, out object? memberValue)
    {
        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key is not null
                    && string.Equals(Convert.ToString(entry.Key, CultureInfo.InvariantCulture), memberName, StringComparison.OrdinalIgnoreCase))
                {
                    memberValue = entry.Value;
                    return true;
                }
            }
        }

        var type = value.GetType();

        var property = type.GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
        if (property is not null)
        {
            memberValue = property.GetValue(value);
            return true;
        }

        var field = type.GetField(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
        if (field is not null)
        {
            memberValue = field.GetValue(value);
            return true;
        }

        memberValue = null;
        return false;
    }

    private static bool TryToInt32(object? value, out int result)
    {
        try
        {
            switch (value)
            {
                case null:
                    result = 0;
                    return false;

                case int intValue:
                    result = intValue;
                    return true;

                case string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                    result = parsed;
                    return true;

                case IConvertible convertible:
                    result = convertible.ToInt32(CultureInfo.InvariantCulture);
                    return true;

                default:
                    result = 0;
                    return false;
            }
        }
        catch
        {
            result = 0;
            return false;
        }
    }

    private static bool TryToBigInteger(object? value, out BigInteger result)
    {
        switch (value)
        {
            case BigInteger bigInteger:
                result = bigInteger;
                return true;

            case byte byteValue:
                result = byteValue;
                return true;

            case short shortValue:
                result = shortValue;
                return true;

            case int intValue:
                result = intValue;
                return true;

            case long longValue:
                result = longValue;
                return true;

            case uint uintValue:
                result = uintValue;
                return true;

            case ulong ulongValue:
                result = ulongValue;
                return true;

            case string text when BigInteger.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;

            default:
                result = BigInteger.Zero;
                return false;
        }
    }

}
