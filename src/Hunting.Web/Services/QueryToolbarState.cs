namespace Hunting.Web.Services;

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

        if (SelectedResultLimit.HasValue && SelectedResultLimit.Value <= 0)
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

        var from = range.Value.From.ToString("yyyy-MM-dd HH:mm:ss");
        var to = range.Value.To?.ToString("yyyy-MM-dd HH:mm:ss");

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
            ? $"Timestamp >= datetime({from})"
            : $"Timestamp >= datetime({from}) and Timestamp <= datetime({to})";
        return query + $"\n| where {kqlClause}";
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

        return query + $"\n| take {SelectedResultLimit.Value}";
    }

    private static bool IsSqlQuery(string query)
        => query.TrimStart().StartsWith(SqlPrefix, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\b(timestamp)\b.*\b(>=|<=|>|<|between)\b|\bwhere\b.*\b(timestamp)\b", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: 250)]
    private static partial Regex TimestampFilterRegex();

    [GeneratedRegex(@"\b(take|limit|top)\s+\d+\b", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: 250)]
    private static partial Regex InlineLimitRegex();
}

public sealed record TimeFilterPreset(string Key, string Label, TimeSpan? Span);