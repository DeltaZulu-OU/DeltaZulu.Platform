namespace DeltaZulu.Platform.Data.Sqlite.Analytics;

internal static class SqliteDateTimeHelpers
{
    internal static string Format(DateTime value) => NormalizeUtc(value).ToString("O");

    internal static string? FormatNullable(DateTime? value) =>
        value is null ? null : Format(value.Value);

    internal static DateTime Parse(string value) =>
        DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);

    internal static DateTime? ParseNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Parse(value);

    internal static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    internal static string? NormalizeLikeSearch(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
