namespace DeltaZulu.Platform.Data.DuckDb.Sql;

internal static class DuckDbSqlText
{
    /// <summary>
    /// Escape an identifier for DuckDB SQL.
    /// Only quotes when the name contains special characters, starts with a digit,
    /// or is empty.
    /// </summary>
    internal static string EscapeIdent(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "\"\"";
        }

        if (name.All(c => char.IsLetterOrDigit(c) || c == '_') && !char.IsDigit(name[0]))
        {
            return name;
        }

        return $"\"{name.Replace("\"", "\"\"")}\"";
    }

    /// <summary>
    /// Escape a possibly schema-qualified name (e.g. <c>silver.v_foo</c>) by
    /// escaping each dot-separated segment independently. Escaping the whole
    /// string would quote the dot and treat it as one identifier.
    /// </summary>
    internal static string EscapeQualifiedIdent(string qualifiedName)
    {
        if (string.IsNullOrEmpty(qualifiedName))
        {
            return EscapeIdent(qualifiedName);
        }

        return string.Join(".", qualifiedName.Split('.').Select(EscapeIdent));
    }

    internal static string EscapeString(string s) => s.Replace("'", "''");

    internal static string IndentSql(string sql)
    {
        var normalized = sql.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        return string.Join(
            Environment.NewLine,
            lines.Select(line => "    " + line));
    }
}