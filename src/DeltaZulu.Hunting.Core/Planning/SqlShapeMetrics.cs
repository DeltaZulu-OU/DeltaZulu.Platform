namespace DeltaZulu.Hunting.Core.Planning;

using System.Text.RegularExpressions;

/// <summary>
/// Lightweight emitted-SQL shape metrics used to track whether planner/emitter
/// changes are reducing avoidable SQL complexity without replacing DuckDB's
/// physical optimizer.
/// </summary>
public sealed record SqlShapeMetrics(
    int CteStageCount,
    int SelectCount,
    int JoinCount,
    int OrderByCount,
    int LimitCount,
    int SqlLength)
{
    public static SqlShapeMetrics FromSql(string sql)
    {
        var normalized = sql ?? string.Empty;
        return new SqlShapeMetrics(
            CteStageCount: Count(normalized, "__kql_stage_"),
            SelectCount: CountWord(normalized, "select"),
            JoinCount: CountWord(normalized, "join"),
            OrderByCount: CountPhrase(normalized, "order by"),
            LimitCount: CountWord(normalized, "limit"),
            SqlLength: normalized.Length);
    }

    private static int Count(string text, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private static int CountWord(string text, string word)
        => Regex.Count(text, $@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static int CountPhrase(string text, string phrase)
        => Regex.Count(text, Regex.Escape(phrase), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
