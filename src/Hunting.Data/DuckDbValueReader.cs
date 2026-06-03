namespace Hunting.Data;

using System.Collections;
using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

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

        if (RequiresStringNormalization(reader.GetDataTypeName(ordinal)))
        {
            return ReadProviderValueAsString(reader, ordinal);
        }

        return reader.GetValue(ordinal);
    }

    private static string? ReadProviderValueAsString(DbDataReader reader, int ordinal)
    {
        object? value;

        try
        {
            value = reader.GetValue(ordinal);
        }
        catch (Exception ex) when (IsProviderMaterializationFailure(ex))
        {
            return ReadStringFallback(reader, ordinal);
        }

        if (value is null || value is DBNull)
        {
            return null;
        }

        if (value is string text)
        {
            return text;
        }

        if (TryFormatInet(value, out var formattedInet))
        {
            return formattedInet;
        }

        if (value is IFormattable formattable && IsScalarLike(value.GetType()))
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        try
        {
            return JsonSerializer.Serialize(value);
        }
        catch (NotSupportedException)
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    private static string? ReadStringFallback(DbDataReader reader, int ordinal)
    {
        try
        {
            return reader.GetString(ordinal);
        }
        catch (Exception ex) when (IsProviderMaterializationFailure(ex))
        {
            try
            {
                return Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
            }
            catch (Exception inner) when (IsProviderMaterializationFailure(inner))
            {
                return null;
            }
        }
    }

    private static bool RequiresStringNormalization(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return false;
        }

        var normalized = typeName.Trim().ToUpperInvariant();

        if (normalized is "JSON" or "UUID" or "INET" or "IPADDR" or "CIDR")
        {
            return true;
        }

        return normalized.StartsWith("STRUCT", StringComparison.Ordinal)
            || normalized.StartsWith("MAP", StringComparison.Ordinal)
            || normalized.StartsWith("UNION", StringComparison.Ordinal)
            || normalized.EndsWith("[]", StringComparison.Ordinal)
            || normalized.Contains("LIST", StringComparison.Ordinal)
            || normalized.Contains("STRUCT(", StringComparison.Ordinal)
            || normalized.Contains("MAP(", StringComparison.Ordinal);
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

    private static bool IsScalarLike(Type type)
    {
        return type.IsPrimitive
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(Guid);
    }

    private static bool IsProviderMaterializationFailure(Exception ex)
    {
        return ex is InvalidCastException
            or NotSupportedException
            or MissingMethodException;
    }
}
