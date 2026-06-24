using System.Globalization;
using System.Text;
using DeltaZulu.Platform.Domain.Analytics.Execution;

namespace DeltaZulu.Platform.Web.Analytics.Exports;

public static class QueryResultCsvExporter
{
    public const string MimeType = "text/csv;charset=utf-8";
    private static readonly char[] anyOf = new[] { '"', ',', '\r', '\n' };

    public static string BuildCsv(QueryResult result, IReadOnlyList<int>? columnIndexes = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        var columns = columnIndexes ?? Enumerable.Range(0, result.ColumnCount).ToArray();
        var builder = new StringBuilder();
        AppendCsvRow(builder, columns.Select(columnIndex => result.Columns[columnIndex].Name));

        for (var rowIndex = 0; rowIndex < result.RowCount; rowIndex++)
        {
            AppendCsvRow(builder, columns.Select(columnIndex => FormatCsvCell(result.GetValue(rowIndex, columnIndex))));
        }

        return builder.ToString();
    }

    public static string NormalizeCsvFileName(string fileName, string fallbackFileName = "query-results.csv")
    {
        var safeName = string.IsNullOrWhiteSpace(fileName)
            ? fallbackFileName
            : fileName.Trim();

        return safeName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            ? safeName
            : $"{safeName}.csv";
    }

    public static string BuildCsvFileName(string queryText)
    {
        var firstLine = queryText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim())
            .FirstOrDefault(static line => !string.IsNullOrWhiteSpace(line));

        var baseName = string.IsNullOrWhiteSpace(firstLine)
            ? "query-results"
            : firstLine.Length <= 60 ? firstLine : firstLine[..60];

        var safeName = new string(baseName.Select(static ch =>
            char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-').ToArray()).Trim('-');

        return NormalizeCsvFileName(string.IsNullOrWhiteSpace(safeName) ? "query-results" : safeName);
    }

    private static void AppendCsvRow(StringBuilder builder, IEnumerable<string> values)
    {
        var first = true;
        foreach (var value in values)
        {
            if (!first)
            {
                builder.Append(',');
            }

            builder.Append(EscapeCsvField(value));
            first = false;
        }

        builder.AppendLine();
    }

    private static string EscapeCsvField(string value)
    {
        if (value.IndexOfAny(anyOf) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string FormatCsvCell(object? value) => value switch {
        null => string.Empty,
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
        bool boolean => boolean ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty
    };
}
