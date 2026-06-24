using System.Text.RegularExpressions;

namespace DeltaZulu.Platform.Web.Analytics.Rendering;

public static partial class RenderClauseQueryValidator
{
    public static int CountRenderCommands(string? queryText)
        => string.IsNullOrWhiteSpace(queryText)
            ? 0
            : RenderCommandRegex().Count(queryText);

    public static bool TryAppendOrReplaceTerminalRender(
        string? queryText,
        string renderClause,
        out string updatedQueryText,
        out string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(renderClause);

        var normalizedQueryText = queryText ?? string.Empty;
        var renderCount = CountRenderCommands(normalizedQueryText);
        if (renderCount > 1)
        {
            updatedQueryText = normalizedQueryText;
            error = "The query already contains multiple render commands. Remove the extra render command before applying the visualization wizard.";
            return false;
        }

        if (renderCount == 1 && !TryStripTerminalRenderClause(normalizedQueryText, out normalizedQueryText))
        {
            updatedQueryText = queryText ?? string.Empty;
            error = "The query already contains a render command that is not the final pipeline operator. Remove it before applying the visualization wizard.";
            return false;
        }

        var baseQuery = normalizedQueryText.TrimEnd();
        updatedQueryText = string.IsNullOrWhiteSpace(baseQuery)
            ? renderClause.Trim()
            : $"{baseQuery}{Environment.NewLine}{renderClause.Trim()}";
        error = string.Empty;
        return true;
    }

    public static bool TryStripTerminalRenderClause(string queryText, out string queryTextWithoutRender)
    {
        queryTextWithoutRender = queryText;
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return false;
        }

        var matches = RenderCommandRegex().Matches(queryText);
        if (matches.Count == 0)
        {
            return false;
        }

        var lastRender = matches[matches.Count - 1];
        var lastPipeIndex = queryText.LastIndexOf('|');
        if (lastPipeIndex != lastRender.Index)
        {
            return false;
        }

        queryTextWithoutRender = queryText[..lastRender.Index];
        return true;
    }

    [GeneratedRegex(@"\|\s*render\b", RegexOptions.IgnoreCase)]
    private static partial Regex RenderCommandRegex();
}
