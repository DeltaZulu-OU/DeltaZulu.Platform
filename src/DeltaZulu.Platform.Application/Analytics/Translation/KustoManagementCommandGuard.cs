using Kusto.Language.Syntax;

namespace DeltaZulu.Platform.Application.Analytics.Translation;

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

    // Every management-command AST node type in Kusto.Language (Command, CustomCommand,
    // UnknownCommand, PartialCommand, BadCommand, CommandBlock, CommandWithClause,
    // CommandWithValueClause, CommandWithPropertyListClause, CommandAndSkippedTokens —
    // verified against the pinned Kusto.Language package version) contains "Command" in its
    // type name; no non-command syntax node does. The previous check matched exact/substring
    // names ("CommandBlock", "Command", "CommandStatement") that no longer correspond to any
    // real type the parser produces, so this check never actually fired.
    private static bool IsManagementCommandNode(SyntaxNode node) =>
        node.GetType().Name.Contains("Command", StringComparison.Ordinal);
}