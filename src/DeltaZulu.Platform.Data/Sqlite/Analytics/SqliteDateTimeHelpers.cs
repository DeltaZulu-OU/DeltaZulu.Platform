namespace DeltaZulu.Platform.Data.Sqlite.Analytics;

public static class SqliteDateTimeHelpers
{
    public static string Format(DateTime value) => NormalizeUtc(value).ToString("O");

    public static string? FormatNullable(DateTime? value) =>
        value is null ? null : Format(value.Value);

    public static DateTime Parse(string value) =>
        DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);

    public static DateTime? ParseNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Parse(value);

    public static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    public static string? NormalizeLikeSearch(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
