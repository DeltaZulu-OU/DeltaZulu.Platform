namespace Hunting.Web.Services;

using System.Globalization;
using System.Text.RegularExpressions;

public sealed partial class QueryToolbarState
{
    private const string SqlPrefix = "select";
    private const int MaxQueryLength = 200_000;

    public string SelectedTimeFilter { get; set; } = "none";
    public DateTime? CustomFrom { get; set; }
    public DateTime? CustomTo { get; set; }
    public int? SelectedResultLimit { get; set; } = null;

    public static IReadOnlyList<int?> ResultLimitOptions { get; } = [null, 10, 20, 50, 100, 500, 1000];

    public static IReadOnlyList<TimeFilterPreset> TimeFilterPresets { get; } =
    [
        new("none", "None", null),
        new("1h", "Last 1 hour", TimeSpan.FromHours(1)),
        new("6h", "Last 6 hours", TimeSpan.FromHours(6)),
        new("24h", "Last 24 hours", TimeSpan.FromHours(24)),
        new("7d", "Last 7 days", TimeSpan.FromDays(7)),
        new("30d", "Last 30 days", TimeSpan.FromDays(30)),
        new("custom", "Custom range", null)
    ];

    public string TimeFilterCaption {
        get {
            var range = GetSelectedTimeRange();
            if (range is null)
            {
                return "No time filter";
            }

            if (range.Value.To is null)
            {
                return $"Since {range.Value.From:yyyy-MM-dd HH:mm} UTC";
            }

            return $"{range.Value.From:yyyy-MM-dd} → {range.Value.To:yyyy-MM-dd} UTC";
        }
    }

    public bool TryBuildEffectiveQuery(string query, out string effectiveQuery, out string warning)
    {
        effectiveQuery = query;
        warning = string.Empty;

        if (string.IsNullOrWhiteSpace(query))
        {
            warning = "Query cannot be empty.";
            return false;
        }

        if (query.Length > MaxQueryLength)
        {
            warning = $"Query is too large ({query.Length} chars). Maximum supported length is {MaxQueryLength}.";
            return false;
        }

        if (SelectedResultLimit <= 0)
        {
            warning = "Result limit must be a positive number.";
            return false;
        }

        if (CustomFrom.HasValue && CustomTo.HasValue && CustomFrom.Value.Date > CustomTo.Value.Date)
        {
            warning = "Custom time range is invalid: From date must be on or before To date.";
            return false;
        }

        var selectedTimeRange = GetSelectedTimeRange();
        var hasInlineTimeFilter = HasInlineTimeFilter(query);
        var hasInlineLimit = HasInlineLimit(query);
        var hasUiTimeFilter = selectedTimeRange is not null;
        var hasUiLimit = SelectedResultLimit.HasValue;

        if (hasUiTimeFilter && hasInlineTimeFilter)
        {
            warning = "Time filter is defined both in the toolbar and in the query. Remove one filter.";
            return false;
        }

        if (hasUiLimit && hasInlineLimit)
        {
            warning = "Result limit is defined both in the toolbar and in the query (take/limit). Remove one limit.";
            return false;
        }

        effectiveQuery = ApplyTimeFilter(query, selectedTimeRange);
        effectiveQuery = ApplyResultLimit(effectiveQuery);
        return true;
    }

    private (DateTime From, DateTime? To)? GetSelectedTimeRange()
    {
        if (SelectedTimeFilter == "none")
        {
            return null;
        }

        if (SelectedTimeFilter == "custom")
        {
            if (!CustomFrom.HasValue)
            {
                return null;
            }

            var from = CustomFrom.Value;
            DateTime? to = null;
            if (CustomTo.HasValue)
            {
                to = CustomTo.Value.Date.AddDays(1).AddTicks(-1);
            }

            return (DateTime.SpecifyKind(from.Date, DateTimeKind.Utc), to is null ? null : DateTime.SpecifyKind(to.Value, DateTimeKind.Utc));
        }

        var preset = TimeFilterPresets.FirstOrDefault(
            p => string.Equals(p.Key, SelectedTimeFilter, StringComparison.OrdinalIgnoreCase));
        if (preset?.Span is null)
        {
            return null;
        }

        return (DateTime.UtcNow - preset.Span.Value, null);
    }

    private static bool HasInlineTimeFilter(string query) => TimestampFilterRegex().IsMatch(query);

    private static bool HasInlineLimit(string query) => InlineLimitRegex().IsMatch(query);

    private string ApplyTimeFilter(string query, (DateTime From, DateTime? To)? range)
    {
        if (range is null)
        {
            return query;
        }

        var from = range.Value.From
            .ToUniversalTime()
            .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        var to = range.Value.To?
            .ToUniversalTime()
            .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        if (IsSqlQuery(query))
        {
            var clause = to is null
                ? $"Timestamp >= TIMESTAMP '{from}'"
                : $"Timestamp BETWEEN TIMESTAMP '{from}' AND TIMESTAMP '{to}'";

            return query.Contains(" where ", StringComparison.OrdinalIgnoreCase)
                ? query + $" AND {clause}"
                : query + $" WHERE {clause}";
        }

        var kqlClause = to is null
            ? $"Timestamp >= datetime('{from}')"
            : $"Timestamp >= datetime('{from}') and Timestamp <= datetime('{to}')";

        return AppendKqlPipelineOperatorBeforeTerminalRender(query, $"where {kqlClause}");
    }

    private string ApplyResultLimit(string query)
    {
        if (!SelectedResultLimit.HasValue)
        {
            return query;
        }

        if (IsSqlQuery(query))
        {
            return query + $" LIMIT {SelectedResultLimit.Value}";
        }

        return AppendKqlPipelineOperatorBeforeTerminalRender(query, $"take {SelectedResultLimit.Value}");
    }

    private static string AppendKqlPipelineOperatorBeforeTerminalRender(string query, string operatorText)
    {
        var segments = SplitKqlPipeline(query);

        if (segments.Count == 0)
        {
            return query;
        }

        var lastSegment = segments[^1].TrimStart();
        var hasTerminalRender = lastSegment.StartsWith("render ", StringComparison.OrdinalIgnoreCase)
            || string.Equals(lastSegment, "render", StringComparison.OrdinalIgnoreCase);

        if (!hasTerminalRender)
        {
            return query.TrimEnd() + "\n| " + operatorText;
        }

        segments.Insert(segments.Count - 1, operatorText);
        return string.Join("\n| ", segments.Select(s => s.Trim()));
    }

    private static List<string> SplitKqlPipeline(string query)
    {
        var segments = new List<string>();
        var start = 0;
        var parenDepth = 0;
        var bracketDepth = 0;
        var braceDepth = 0;
        var inSingleQuotedString = false;
        var inDoubleQuotedString = false;
        var inVerbatimString = false;

        for (var i = 0; i < query.Length; i++)
        {
            var current = query[i];
            var previous = i > 0 ? query[i - 1] : '\0';
            var next = i + 1 < query.Length ? query[i + 1] : '\0';

            if (inSingleQuotedString)
            {
                if (current == '\'' && next == '\'')
                {
                    i++;
                    continue;
                }

                if (current == '\'' && previous != '\\')
                {
                    inSingleQuotedString = false;
                }

                continue;
            }

            if (inDoubleQuotedString)
            {
                if (current == '"' && next == '"')
                {
                    i++;
                    continue;
                }

                if (current == '"' && previous != '\\')
                {
                    inDoubleQuotedString = false;
                }

                continue;
            }

            if (inVerbatimString)
            {
                if (current == '"' && next == '"')
                {
                    i++;
                    continue;
                }

                if (current == '"')
                {
                    inVerbatimString = false;
                }

                continue;
            }

            if (current == '@' && next == '"')
            {
                inVerbatimString = true;
                i++;
                continue;
            }

            if (current == '\'')
            {
                inSingleQuotedString = true;
                continue;
            }

            if (current == '"')
            {
                inDoubleQuotedString = true;
                continue;
            }

            switch (current)
            {
                case '(':
                    parenDepth++;
                    break;
                case ')':
                    parenDepth = Math.Max(0, parenDepth - 1);
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    bracketDepth = Math.Max(0, bracketDepth - 1);
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}':
                    braceDepth = Math.Max(0, braceDepth - 1);
                    break;
                case '|':
                    if (parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
                    {
                        segments.Add(query[start..i].Trim());
                        start = i + 1;
                    }

                    break;
            }
        }

        segments.Add(query[start..].Trim());
        return segments.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }

    private static bool IsSqlQuery(string query)
        => query.TrimStart().StartsWith(SqlPrefix, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\b(timestamp)\b.*\b(>=|<=|>|<|between)\b|\bwhere\b.*\b(timestamp)\b", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: 250)]
    private static partial Regex TimestampFilterRegex();

    [GeneratedRegex(@"\b(take|limit|top)\s+\d+\b", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: 250)]
    private static partial Regex InlineLimitRegex();
}

public sealed record TimeFilterPreset(string Key, string Label, TimeSpan? Span);