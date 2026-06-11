
using DeltaZulu.Platform.Application.Analytics.Rendering.Tabular;

namespace DeltaZulu.Platform.Application.Analytics.Rendering.Services;
public static class RenderTypeClassifier
{
    private static readonly HashSet<string> NumericTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "TINYINT",
        "SMALLINT",
        "INTEGER",
        "BIGINT",
        "HUGEINT",
        "UTINYINT",
        "USMALLINT",
        "UINTEGER",
        "UBIGINT",
        "UHUGEINT",
        "FLOAT",
        "DOUBLE",
        "DECIMAL",
        "REAL"
    };

    private static readonly HashSet<string> TemporalTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "DATE",
        "TIME",
        "TIMESTAMP",
        "TIMESTAMP_S",
        "TIMESTAMP_MS",
        "TIMESTAMP_NS",
        "TIMESTAMP WITH TIME ZONE",
        "TIMESTAMPTZ",
        "TIME WITH TIME ZONE",
        "TIMETZ",
        "DATETIME"
    };

    public static RenderColumn Classify(string name, string? typeName = null, Type? clrType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalizedTypeName = NormalizeTypeName(typeName);
        var isNumeric = IsNumeric(normalizedTypeName, clrType);
        var isTemporal = IsTemporal(normalizedTypeName, clrType);

        return new RenderColumn
        {
            Name = name,
            TypeName = typeName,
            ClrType = clrType,
            IsNumeric = isNumeric,
            IsTemporal = isTemporal,
            IsCategorical = IsCategorical(normalizedTypeName, clrType, isNumeric, isTemporal)
        };
    }

    public static bool IsNumeric(string? typeName, Type? clrType = null)
    {
        if (!string.IsNullOrWhiteSpace(typeName) && NumericTypeNames.Contains(NormalizeTypeName(typeName)!))
        {
            return true;
        }

        var type = UnwrapNullable(clrType);
        return type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal);
    }

    public static bool IsTemporal(string? typeName, Type? clrType = null)
    {
        if (!string.IsNullOrWhiteSpace(typeName) && TemporalTypeNames.Contains(NormalizeTypeName(typeName)!))
        {
            return true;
        }

        var type = UnwrapNullable(clrType);
        return type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(DateOnly)
            || type == typeof(TimeOnly)
            || type == typeof(TimeSpan);
    }

    public static bool IsCategorical(string? typeName, Type? clrType = null)
        => IsCategorical(NormalizeTypeName(typeName), clrType, IsNumeric(typeName, clrType), IsTemporal(typeName, clrType));

    private static bool IsCategorical(string? normalizedTypeName, Type? clrType, bool isNumeric, bool isTemporal)
    {
        if (isNumeric || isTemporal)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(normalizedTypeName))
        {
            return normalizedTypeName is "VARCHAR" or "CHAR" or "TEXT" or "STRING" or "UUID" or "BOOLEAN" or "BOOL" or "ENUM";
        }

        var type = UnwrapNullable(clrType);
        return type == typeof(string)
            || type == typeof(char)
            || type == typeof(Guid)
            || type == typeof(bool)
            || type?.IsEnum == true;
    }

    private static string? NormalizeTypeName(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        var trimmed = typeName.Trim();
        var precisionStart = trimmed.IndexOf('(');
        if (precisionStart >= 0)
        {
            trimmed = trimmed[..precisionStart].TrimEnd();
        }

        return trimmed.ToUpperInvariant();
    }

    private static Type? UnwrapNullable(Type? type)
        => type is null ? null : Nullable.GetUnderlyingType(type) ?? type;
}