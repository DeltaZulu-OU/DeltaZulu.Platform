namespace DeltaZulu.Platform.Domain.Analytics.Schema;

/// <summary>
/// Renders the DuckDB expression that performs a declared <see cref="ParserCanonicalization" />.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ParserCanonicalization" /> was declared on <see cref="ParserFieldContract" /> and
/// checked for consistency by <see cref="LogicalSchemaValidator" /> — <c>Utc</c> is rejected on a
/// non-<c>Timestamp</c> family — but nothing ever performed the canonicalisation. It was validated
/// and never applied, so two rows carrying the same address or the same instant in different
/// textual forms stayed different values.
/// </para>
/// <para>
/// The canonicalisation is DECLARED by the field, never inferred from the value. This class turns
/// that declaration into SQL; it does not inspect data to decide what to do.
/// </para>
/// </remarks>
public static class ParserCanonicalizer
{
    /// <summary>
    /// Returns <paramref name="sourceExpression" /> wrapped in the SQL that applies
    /// <paramref name="canonicalization" />, or unchanged when none is declared.
    /// </summary>
    /// <remarks>
    /// The arms are exhaustive over <see cref="ParserCanonicalization" /> with no <c>_ =&gt;</c>
    /// default, so adding a member is a compile-time gap rather than a value that silently passes
    /// through uncanonicalised — which is the failure this class exists to remove.
    /// </remarks>
    public static string ToDuckDbExpression(ParserCanonicalization canonicalization, string sourceExpression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceExpression);

        return canonicalization switch
        {
            ParserCanonicalization.None => sourceExpression,

            // Measured, not assumed: DuckDB reads offset-bearing text correctly whatever the
            // session timezone ('...T12:00:00+02:00' yields 10:00:00 under both Etc/UTC and
            // America/New_York), but reads OFFSETLESS text in the session timezone — the same
            // literal yielded 12:00:00 under UTC and 17:00:00 under America/New_York. The result
            // is therefore only correct while the session is pinned to UTC, which
            // DuckDbConnectionFactory now does on every connection. CON-0001 makes KQL datetime
            // UTC-only, so the cast lands on a zoneless TIMESTAMP rather than a TIMESTAMPTZ, and
            // no DateTimeOffset is created anywhere on this path.
            ParserCanonicalization.Utc =>
                $"CAST(CAST({sourceExpression} AS TIMESTAMPTZ) AT TIME ZONE 'UTC' AS TIMESTAMP)",

            // Strip every non-hex character, then regroup into colon-separated pairs. Doing it in
            // two steps rather than replacing separators means the Cisco dotted form
            // (aabb.ccdd.eeff), the dashed form and the colon form all converge on one spelling;
            // a plain replace would leave the dotted form as aabb:ccdd:eeff.
            ParserCanonicalization.MacLowerColon =>
                $"regexp_replace(regexp_replace(lower({sourceExpression}), '[^0-9a-f]', '', 'g'), " +
                "'(..)(..)(..)(..)(..)(..)', '\\1:\\2:\\3:\\4:\\5:\\6')",

            // DuckDB's native INET stores an address rather than its spelling, so the cast is the
            // compression: '2001:0db8:0000:0000:0000:0000:0000:0001' reads back as '2001:db8::1'.
            // Now that IpAddress projects to INET this is usually a no-op on an already-native
            // column, and remains correct when the source is text.
            ParserCanonicalization.Ipv6Compressed =>
                $"CAST({sourceExpression} AS INET)",
        };
    }

    /// <summary>
    /// Returns the canonicalising expression for <paramref name="field" />, reading from the
    /// column named after the field. Fields with no parser contract are read unchanged.
    /// </summary>
    public static string ToDuckDbExpression(LogicalFieldDef field)
    {
        ArgumentNullException.ThrowIfNull(field);

        var column = $"\"{field.Name.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        return field.Parser is not { } parser
            ? column
            : ToDuckDbExpression(parser.Canonicalization, column);
    }
}

/// <summary>One projected column and the expression that produces it.</summary>
public sealed record CanonicalizedColumn(string TargetColumn, string Expression, LogicalFieldType Type);

/// <summary>
/// The SELECT list that reads a source relation and applies every declared canonicalisation.
/// </summary>
public sealed record CanonicalizedProjection(
    string RegistryKey,
    string SourceObject,
    IReadOnlyList<CanonicalizedColumn> Columns);
