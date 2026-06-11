namespace DeltaZulu.Platform.Domain.Hunting.Translation;

using Kusto.Language.Syntax;

/// <summary>Blocks executable Kusto management dot-commands before translation.</summary>
internal static class KustoManagementCommandGuard
{
    public static bool ContainsExecutableCommandText(string kql)
    {
        var inSingleQuotedString = false;
        var inDoubleQuotedString = false;
        var inMultilineString = false;
        var inLineComment = false;
        var inBlockComment = false;
        var commandStartPossible = true;

        for (var i = 0; i < kql.Length; i++)
        {
            var c = kql[i];
            var next = i + 1 < kql.Length ? kql[i + 1] : '\0';

            if (inMultilineString)
            {
                if (c == '`' && i + 2 < kql.Length && kql[i + 1] == '`' && kql[i + 2] == '`')
                {
                    inMultilineString = false;
                    i += 2;
                    commandStartPossible = false;
                }
                continue;
            }

            if (inLineComment)
            {
                if (c is '\r' or '\n')
                {
                    inLineComment = false;
                    commandStartPossible = true;
                }
                continue;
            }

            if (inBlockComment)
            {
                if (c == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++;
                }
                continue;
            }

            if (inSingleQuotedString)
            {
                if (c == '\\') { i++; continue; }
                if (c == '\'' && next == '\'') { i++; continue; }
                if (c == '\'') { inSingleQuotedString = false; commandStartPossible = false; }
                continue;
            }

            if (inDoubleQuotedString)
            {
                if (c == '\\') { i++; continue; }
                if (c == '"' && next == '"') { i++; continue; }
                if (c == '"') { inDoubleQuotedString = false; commandStartPossible = false; }
                continue;
            }

            if (c == '`' && i + 2 < kql.Length && kql[i + 1] == '`' && kql[i + 2] == '`')
            {
                inMultilineString = true;
                i += 2;
                commandStartPossible = false;
                continue;
            }
            if (c == '/' && next == '/') { inLineComment = true; i++; continue; }
            if (c == '/' && next == '*') { inBlockComment = true; i++; continue; }
            if (c == '"') { inDoubleQuotedString = true; commandStartPossible = false; continue; }
            if (c == '\'') { inSingleQuotedString = true; commandStartPossible = false; continue; }
            if (c == ';') { commandStartPossible = true; continue; }
            if (c is '\r' or '\n') { commandStartPossible = true; continue; }
            if (char.IsWhiteSpace(c)) { continue; }
            if (commandStartPossible && c == '.') { return true; }
            commandStartPossible = false;
        }
        return false;
    }

    public static bool ContainsExecutableCommand(SyntaxNode root) => root.GetDescendants<SyntaxNode>()
        .Any(IsManagementCommandNode);

    private static bool IsManagementCommandNode(SyntaxNode node)
    {
        var typeName = node.GetType().Name;
        return typeName.Equals("CommandBlock", StringComparison.Ordinal)
            || typeName.Equals("Command", StringComparison.Ordinal)
            || typeName.Equals("CommandStatement", StringComparison.Ordinal)
            || typeName.Contains("CommandBlock", StringComparison.Ordinal)
            || typeName.Contains("CommandStatement", StringComparison.Ordinal);
    }
}