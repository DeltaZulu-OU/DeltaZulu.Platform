namespace Hunting.Web.Services;

using System.Text.RegularExpressions;

public sealed class QueryToolbarState
{
    public string SelectedTimeFilter { get; set; } = "none";
    public string? CustomFromText { get; set; }
    public string? CustomToText { get; set; }
    public int? SelectedResultLimit { get; set; } = 100;

    public static IReadOnlyList<int?> ResultLimitOptions { get; } = [10, 20, 50, 100, 500, 1000, null];

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

    public string TimeFilterCaption
    {
        get
        {
            var range = GetSelectedTimeRange();
            if (range is null) return "No time filter";
            if (range.Value.To is null) return $"Since {range.Value.From:yyyy-MM-dd HH:mm} UTC";
            return $"{range.Value.From:yyyy-MM-dd} → {range.Value.To:yyyy-MM-dd} UTC";
        }
    }

    public bool TryBuildEffectiveQuery(string query, out string effectiveQuery, out string warning)
    {
        effectiveQuery = query;
        warning = string.Empty;

        var hasInlineTimeFilter = HasInlineTimeFilter(query);
        var hasInlineLimit = HasInlineLimit(query);
        var hasUiTimeFilter = GetSelectedTimeRange() is not null;
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

        effectiveQuery = ApplyTimeFilter(query);
        effectiveQuery = ApplyResultLimit(effectiveQuery);
        return true;
    }

    private (DateTime From, DateTime? To)? GetSelectedTimeRange()
    {
        var preset = TimeFilterPresets.FirstOrDefault(p => p.Key == SelectedTimeFilter);
        if (SelectedTimeFilter == "none") return null;

        if (SelectedTimeFilter == "custom")
        {
            if (!DateTime.TryParse(CustomFromText, out var from)) return null;

            DateTime? to = null;
            if (DateTime.TryParse(CustomToText, out var customTo))
            {
                to = customTo.Date.AddDays(1).AddTicks(-1);
            }

            return (DateTime.SpecifyKind(from.Date, DateTimeKind.Utc), to is null ? null : DateTime.SpecifyKind(to.Value, DateTimeKind.Utc));
        }

        if (preset?.Span is null) return null;
        return (DateTime.UtcNow - preset.Span.Value, null);
    }

    private static bool HasInlineTimeFilter(string query)
        => Regex.IsMatch(
            query,
            @"\b(timestamp)\b.*\b(>=|<=|>|<|between)\b|\bwhere\b.*\b(timestamp)\b",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static bool HasInlineLimit(string query)
        => Regex.IsMatch(query, @"\b(take|limit|top)\s+\d+\b", RegexOptions.IgnoreCase);

    private string ApplyTimeFilter(string query)
    {
        var range = GetSelectedTimeRange();
        if (range is null) return query;

        var from = range.Value.From.ToString("yyyy-MM-dd HH:mm:ss");
        var to = range.Value.To?.ToString("yyyy-MM-dd HH:mm:ss");

        if (query.TrimStart().StartsWith("select", StringComparison.OrdinalIgnoreCase))
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
        if (!SelectedResultLimit.HasValue) return query;

        if (query.TrimStart().StartsWith("select", StringComparison.OrdinalIgnoreCase))
        {
            return query + $" LIMIT {SelectedResultLimit.Value}";
        }

        return query + $"\n| take {SelectedResultLimit.Value}";
    }
}

public sealed record TimeFilterPreset(string Key, string Label, TimeSpan? Span);
