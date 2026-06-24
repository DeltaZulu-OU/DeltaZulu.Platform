using System.Globalization;

namespace DeltaZulu.Platform.Web.Analytics;

internal static class CellFormatter
{
    internal static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            bool boolean => boolean ? "true" : "false",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    internal static string FormatDisplay(object? value, int maxLength = 200)
    {
        if (value is null) return string.Empty;
        if (value is DateTimeOffset dto) return dto.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        if (value is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        if (value is bool b) return b ? "true" : "false";
        var s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        return s.Length > maxLength ? string.Concat(s.AsSpan(0, maxLength), "…") : s;
    }
}
