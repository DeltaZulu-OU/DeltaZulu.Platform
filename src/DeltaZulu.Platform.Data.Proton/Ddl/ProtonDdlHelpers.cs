namespace DeltaZulu.Platform.Data.Proton.Ddl;

internal static class ProtonDdlHelpers
{
    /// <summary>
    /// Backtick-quotes a Proton identifier or a two-part "db.name" reference.
    /// Plain alphanumeric+underscore names pass through without quoting.
    /// </summary>
    internal static string QuoteName(string name)
    {
        if (!name.Contains('.'))
            return QuoteIdentifier(name);
        var dot = name.IndexOf('.');
        return $"{QuoteIdentifier(name[..dot])}.{QuoteIdentifier(name[(dot + 1)..])}";
    }

    internal static string QuoteIdentifier(string id)
    {
        if (id.Length > 0 && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            return id;
        return $"`{id.Replace("`", "``")}`";
    }

    internal static string EscapeSingleQuote(string s) => s.Replace("'", "''");
}
