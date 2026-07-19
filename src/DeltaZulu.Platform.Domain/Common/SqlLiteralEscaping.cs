namespace DeltaZulu.Platform.Domain.Common;

/// <summary>
/// Shared escaping for SQL string literals emitted by hand (DuckDB and Proton/ClickHouse both
/// use a doubled single-quote to escape an embedded quote). Backend-specific emitters still own
/// everything else about their dialect; this covers only the one escaping rule they share.
/// </summary>
public static class SqlLiteralEscaping
{
    public static string EscapeSingleQuotes(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}
