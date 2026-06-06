namespace Hunting.Core.DuckDbSql;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using QueryModel;

internal sealed class DuckDbScalarEmitter
{
    private readonly DuckDbEmitterContext _context;
    private readonly Func<FunctionCall, string> _emitFunction;

    internal DuckDbScalarEmitter(
        DuckDbEmitterContext context,
        Func<FunctionCall, string> emitFunction)
    {
        _context = context;
        _emitFunction = emitFunction;
    }

    #region Scalar expressions

    internal string EmitScalar(ScalarExpr expr) => expr switch
    {
        // Check scalar let binding before emitting as a column identifier.
        // KQL: let cutoff = ago(7d); T | where Timestamp > cutoff
        // Without substitution: WHERE Timestamp > cutoff (undefined column)
        // With substitution:    WHERE Timestamp > (current_timestamp - INTERVAL '7 days')
        ColumnRef { Qualifier: JoinSide.Left } col when _context.JoinLeftAlias is not null
                    => $"{_context.JoinLeftAlias}.{DuckDbSqlText.EscapeIdent(col.Name)}",
        ColumnRef { Qualifier: JoinSide.Right } col when _context.JoinRightAlias is not null
                    => $"{_context.JoinRightAlias}.{DuckDbSqlText.EscapeIdent(col.Name)}",
        ColumnRef col when _context.ScalarBindings.TryGetValue(col.Name, out var bound) => bound,
        ColumnRef col => DuckDbSqlText.EscapeIdent(col.Name),
        LiteralScalar { Value: DateTime dt } => EmitTimestampLiteral(dt),
        LiteralScalar lit => EmitLiteral(lit),
        BinaryScalar bin => EmitBinary(bin),
        UnaryScalar un => EmitUnary(un),
        FunctionCall fn => _emitFunction(fn),
        CaseScalar cs => EmitCase(cs),
        StarExpr => "*",
        WindowScalarExpr win => EmitWindow(win),
        _ => throw new NotSupportedException($"Unsupported ScalarExpr type: {expr.GetType().Name}")
    };

    private string EmitLiteral(LiteralScalar lit)
    {
        if (lit.Value is null || lit.Kind == LiteralKind.Null)
        {
            return "NULL";
        }

        return lit.Kind switch
        {
            LiteralKind.String => $"'{DuckDbSqlText.EscapeString(lit.Value.ToString()!)}'",
            LiteralKind.Bool => Convert.ToBoolean(lit.Value) ? "TRUE" : "FALSE",
            LiteralKind.Timespan => EmitTimespan(lit.Value),
            LiteralKind.DateTime => $"TIMESTAMP '{lit.Value}'",
            _ => lit.Value.ToString()!
        };
    }

    private string EmitBinary(BinaryScalar bin)
    {
        var left = EmitScalar(bin.Left);

        if (bin.Op is ScalarBinaryOp.In or ScalarBinaryOp.NotIn)
        {
            return EmitInBinary(bin, left);
        }

        var right = EmitScalar(bin.Right);

        // For LIKE/ILIKE pattern operators, the right-hand side must be a
        // literal-escaped pattern — % and _ in the search term are not wildcards.
        var escaped = bin.Op switch
        {
            ScalarBinaryOp.Contains or ScalarBinaryOp.NotContains
            or ScalarBinaryOp.ContainsCs or ScalarBinaryOp.NotContainsCs
            or ScalarBinaryOp.StartsWith or ScalarBinaryOp.NotStartsWith
            or ScalarBinaryOp.StartsWithCs or ScalarBinaryOp.NotStartsWithCs
            or ScalarBinaryOp.EndsWith or ScalarBinaryOp.NotEndsWith
            or ScalarBinaryOp.EndsWithCs or ScalarBinaryOp.NotEndsWithCs
                => EscapeLikePattern(bin.Right, right),
            _ => right
        };

        return bin.Op switch
        {
            ScalarBinaryOp.Eq => $"({left} = {right})",
            ScalarBinaryOp.Neq => $"({left} != {right})",
            ScalarBinaryOp.Lt => $"({left} < {right})",
            ScalarBinaryOp.Lte => $"({left} <= {right})",
            ScalarBinaryOp.Gt => $"({left} > {right})",
            ScalarBinaryOp.Gte => $"({left} >= {right})",
            ScalarBinaryOp.And => $"({left} AND {right})",
            ScalarBinaryOp.Or => $"({left} OR {right})",
            ScalarBinaryOp.Add => $"({left} + {right})",
            ScalarBinaryOp.Sub => $"({left} - {right})",
            ScalarBinaryOp.Mul => $"({left} * {right})",
            ScalarBinaryOp.Div => $"({left} / {right})",
            ScalarBinaryOp.Mod => $"({left} % {right})",

            ScalarBinaryOp.Contains => $"({left} ILIKE '%' || {escaped} || '%' ESCAPE '\\')",
            ScalarBinaryOp.NotContains => $"({left} NOT ILIKE '%' || {escaped} || '%' ESCAPE '\\')",
            ScalarBinaryOp.ContainsCs => $"({left} LIKE '%' || {escaped} || '%' ESCAPE '\\')",
            ScalarBinaryOp.NotContainsCs => $"({left} NOT LIKE '%' || {escaped} || '%' ESCAPE '\\')",
            ScalarBinaryOp.StartsWith => $"({left} ILIKE {escaped} || '%' ESCAPE '\\')",
            ScalarBinaryOp.NotStartsWith => $"({left} NOT ILIKE {escaped} || '%' ESCAPE '\\')",
            ScalarBinaryOp.StartsWithCs => $"({left} LIKE {escaped} || '%' ESCAPE '\\')",
            ScalarBinaryOp.NotStartsWithCs => $"({left} NOT LIKE {escaped} || '%' ESCAPE '\\')",
            ScalarBinaryOp.EndsWith => $"({left} ILIKE '%' || {escaped} ESCAPE '\\')",
            ScalarBinaryOp.NotEndsWith => $"({left} NOT ILIKE '%' || {escaped} ESCAPE '\\')",
            ScalarBinaryOp.EndsWithCs => $"({left} LIKE '%' || {escaped} ESCAPE '\\')",
            ScalarBinaryOp.NotEndsWithCs => $"({left} NOT LIKE '%' || {escaped} ESCAPE '\\')",

            ScalarBinaryOp.Has or ScalarBinaryOp.NotHas
            or ScalarBinaryOp.HasCs or ScalarBinaryOp.NotHasCs
            or ScalarBinaryOp.HasPrefix or ScalarBinaryOp.NotHasPrefix
            or ScalarBinaryOp.HasPrefixCs or ScalarBinaryOp.NotHasPrefixCs
            or ScalarBinaryOp.HasSuffix or ScalarBinaryOp.NotHasSuffix
            or ScalarBinaryOp.HasSuffixCs or ScalarBinaryOp.NotHasSuffixCs
                => EmitBinaryHas(bin, left),

            ScalarBinaryOp.MatchesRegex => $"regexp_matches({left}, {right}, 'c')",
            ScalarBinaryOp.NotMatchesRegex => $"(NOT regexp_matches({left}, {right}, 'c'))",

            _ => throw new NotSupportedException($"Unsupported binary op: {bin.Op}")
        };
    }

    private string EmitInBinary(BinaryScalar bin, string left)
    {
        if (bin.Right is not ListScalar list)
        {
            throw new NotSupportedException(
                $"{bin.Op} requires ListScalar RHS, got {bin.Right.GetType().Name}");
        }

        if (list.Items.Count == 0)
        {
            throw new NotSupportedException($"{bin.Op} requires at least one list item");
        }

        var items = list.Items
            .Select(EmitScalar);

        var sqlOp = bin.Op == ScalarBinaryOp.NotIn
            ? "NOT IN"
            : "IN";

        return $"({left} {sqlOp} ({string.Join(", ", items)}))";
    }

    private string EmitUnary(UnaryScalar un)
    {
        var operand = EmitScalar(un.Operand);
        return un.Op switch
        {
            ScalarUnaryOp.Not => $"NOT ({operand})",
            ScalarUnaryOp.Negate => $"(-{operand})",
            _ => throw new NotSupportedException($"Unsupported unary op: {un.Op}")
        };
    }

    private string EmitBinaryHas(BinaryScalar bin, string left)
    {
        // The RHS term must be treated as literal text — regex metacharacters must be
        // escaped before being embedded in the boundary pattern.
        // KQL: col has "c++" must NOT match "c" followed by any number of plus signs.
        var rhs = bin.Right;
        var escapedCs = EscapeRegexTerm(rhs, EmitScalar(rhs));  // case-sensitive escaped term

        // Case-insensitive: lowercase the escaped term at generation time for literals
        string escapedCi;
        if (rhs is LiteralScalar { Kind: LiteralKind.String, Value: string s })
        {
            var lower = s.ToLowerInvariant();
            var escaped = Regex.Escape(lower);
            escapedCi = $"'{DuckDbSqlText.EscapeString(escaped)}'";
        }
        else
        {
            escapedCi = $"lower(regexp_escape({EmitScalar(rhs)}))";
        }

        return bin.Op switch
        {
            ScalarBinaryOp.Has =>
                $"regexp_matches(lower({left}), '(^|[^[:alnum:]])' || {escapedCi} || '([^[:alnum:]]|$)')",
            ScalarBinaryOp.NotHas =>
                $"(NOT regexp_matches(lower({left}), '(^|[^[:alnum:]])' || {escapedCi} || '([^[:alnum:]]|$)'))",
            ScalarBinaryOp.HasCs =>
                $"regexp_matches({left}, '(^|[^[:alnum:]])' || {escapedCs} || '([^[:alnum:]]|$)', 'c')",
            ScalarBinaryOp.NotHasCs =>
                $"(NOT regexp_matches({left}, '(^|[^[:alnum:]])' || {escapedCs} || '([^[:alnum:]]|$)', 'c'))",
            ScalarBinaryOp.HasPrefix =>
                $"regexp_matches(lower({left}), '(^|[^[:alnum:]])' || {escapedCi})",
            ScalarBinaryOp.NotHasPrefix =>
                $"(NOT regexp_matches(lower({left}), '(^|[^[:alnum:]])' || {escapedCi}))",
            ScalarBinaryOp.HasPrefixCs =>
                $"regexp_matches({left}, '(^|[^[:alnum:]])' || {escapedCs}, 'c')",
            ScalarBinaryOp.NotHasPrefixCs =>
                $"(NOT regexp_matches({left}, '(^|[^[:alnum:]])' || {escapedCs}, 'c'))",
            ScalarBinaryOp.HasSuffix =>
                $"regexp_matches(lower({left}), {escapedCi} || '([^[:alnum:]]|$)')",
            ScalarBinaryOp.NotHasSuffix =>
                $"(NOT regexp_matches(lower({left}), {escapedCi} || '([^[:alnum:]]|$)'))",
            ScalarBinaryOp.HasSuffixCs =>
                $"regexp_matches({left}, {escapedCs} || '([^[:alnum:]]|$)', 'c')",
            ScalarBinaryOp.NotHasSuffixCs =>
                $"(NOT regexp_matches({left}, {escapedCs} || '([^[:alnum:]]|$)', 'c'))",
            _ => throw new NotSupportedException($"Not a has operator: {bin.Op}")
        };
    }

    private string EmitCase(CaseScalar cs)
    {
        var sb = new StringBuilder("CASE");
        foreach (var (when, then) in cs.Branches)
        {
            sb.Append($" WHEN {EmitScalar(when)} THEN {EmitScalar(then)}");
        }
        sb.Append($" ELSE {EmitScalar(cs.Else)} END");
        return sb.ToString();
    }

    #endregion Scalar expressions

    #region Window expressions

    private string EmitWindow(WindowScalarExpr win)
    {
        var fnName = MapWindowFunction(win.FunctionName);
        var args = win.Args.Select(EmitScalar).ToList();

        var fnCall = fnName switch
        {
            "count" when args.Count == 0 => "count(*)",
            _ when args.Count == 0 => $"{fnName}()",
            _ => $"{fnName}({string.Join(", ", args)})"
        };

        var sb = new StringBuilder(fnCall);
        sb.Append(" OVER (");

        if (win.Window.PartitionBy.Count > 0)
        {
            sb.Append("PARTITION BY ");
            sb.AppendJoin(", ", win.Window.PartitionBy.Select(EmitScalar));
            if (win.Window.OrderBy.Count > 0)
            {
                sb.Append(' ');
            }
        }

        if (win.Window.OrderBy.Count > 0)
        {
            sb.Append("ORDER BY ");
            sb.AppendJoin(", ", win.Window.OrderBy.Select(EmitSortExpr));
        }

        if (win.Window.Frame is { } frame)
        {
            sb.Append(' ');
            sb.Append(frame.Type == WindowFrameType.Rows ? "ROWS" : "RANGE");
            sb.Append(" BETWEEN ");
            sb.Append(EmitWindowBound(frame.Start));
            sb.Append(" AND ");
            sb.Append(EmitWindowBound(frame.End));
        }

        sb.Append(')');
        return sb.ToString();
    }

    private static string MapWindowFunction(string kustoName) => kustoName.ToLowerInvariant() switch
    {
        "lag" => "lag",
        "lead" => "lead",
        "row_number" => "row_number",
        "dense_rank" or "row_rank_dense" => "dense_rank",
        "rank" or "row_rank_min" => "rank",
        "sum" => "sum",
        "count" => "count",
        "first_value" => "first_value",
        "last_value" => "last_value",
        "nth_value" => "nth_value",
        _ => kustoName
    };

    private string EmitWindowBound(WindowBound bound) => bound.Kind switch
    {
        WindowBoundKind.UnboundedPreceding => "UNBOUNDED PRECEDING",
        WindowBoundKind.Preceding when bound.Offset is not null =>
            $"{EmitScalar(bound.Offset)} PRECEDING",
        WindowBoundKind.CurrentRow => "CURRENT ROW",
        WindowBoundKind.Following when bound.Offset is not null =>
            $"{EmitScalar(bound.Offset)} FOLLOWING",
        WindowBoundKind.UnboundedFollowing => "UNBOUNDED FOLLOWING",
        _ => throw new NotSupportedException($"Invalid window bound: {bound.Kind}")
    };

    #endregion Window expressions

    #region Sort expression

    /// <summary>
    /// Tabular ORDER BY term. Mirrors <see cref="EmitSortExpr"/> but drops the
    /// NULLS modifier when the analyst did not request one and the sort key is
    /// provably non-nullable — null ordering is then unobservable, so the
    /// explicit modifier is noise. Window ORDER BY clauses keep explicit NULLS
    /// ordering via <see cref="EmitSortExpr"/>.
    /// </summary>
    internal string EmitTabularSortExpr(SortExpr sort, RelNode sortInput)
    {
        if (sort.Nulls == NullOrder.Default
            && sort.Expression is ColumnRef col
            && IsNonNullableColumn(sortInput, col.Name))
        {
            var c = EmitScalar(sort.Expression);
            var d = sort.Direction == SortDirection.Desc ? " DESC" : " ASC";
            return $"{c}{d}";
        }

        return EmitSortExpr(sort);
    }

    /// <summary>
    /// True when <paramref name="column"/> is provably non-nullable at the
    /// output of <paramref name="node"/>. Recognizes count-family aggregates and
    /// looks through row- and value-preserving operators. Any operator that
    /// could introduce nulls or redefine the column (project/extend/distinct/
    /// join) conservatively ends the search.
    /// </summary>
    private static bool IsNonNullableColumn(RelNode node, string column)
    {
        switch (node)
        {
            case AggregateNode agg:
                foreach (var a in agg.Aggregates)
                {
                    if (string.Equals(a.Alias, column, StringComparison.OrdinalIgnoreCase))
                    {
                        return IsNonNullableAggregate(a.Expression);
                    }
                }
                return false;

            case FilterNode f:
                return IsNonNullableColumn(f.Input, column);

            case SortNode s:
                return IsNonNullableColumn(s.Input, column);

            case LimitNode l:
                return IsNonNullableColumn(l.Input, column);

            default:
                return false;
        }
    }

    /// <summary>
    /// Count-family aggregates always return a non-null integer (zero for empty
    /// groups), so a column produced by one cannot be NULL.
    /// </summary>
    private static bool IsNonNullableAggregate(ScalarExpr expr) =>
        expr is FunctionCall fn
        && fn.Name.ToLowerInvariant() is "count" or "countif" or "dcount" or "dcountif";

    private string EmitSortExpr(SortExpr sort)
    {
        var col = EmitScalar(sort.Expression);
        var dir = sort.Direction == SortDirection.Desc ? " DESC" : " ASC";
        // Spec §11.3: KQL defaults are DESC NULLS LAST and ASC NULLS FIRST.
        // DuckDB defaults are ASC NULLS LAST (mismatches ASC case).
        // Always emit explicit NULLS ordering to match KQL semantics.
        var nulls = sort.Nulls switch
        {
            NullOrder.First => " NULLS FIRST",
            NullOrder.Last => " NULLS LAST",
            // KQL default: asc → NULLS FIRST, desc → NULLS LAST
            _ => sort.Direction == SortDirection.Asc ? " NULLS FIRST" : " NULLS LAST"
        };
        return $"{col}{dir}{nulls}";
    }

    #endregion Sort expression

    #region Projection

    internal string EmitProjection(ProjectionExpr proj)
    {
        var expr = EmitScalar(proj.Expression);
        if (proj.Expression is ColumnRef col && col.Name == proj.Alias)
        {
            return expr; // no alias needed
        }

        return $"{expr} AS {DuckDbSqlText.EscapeIdent(proj.Alias)}";
    }

    #endregion Projection

    #region Timespan

    /// <summary>
    /// Convert a timespan value to DuckDB INTERVAL literal.
    /// Handles both KQL string form ("7d", "2h") and .NET TimeSpan objects.
    /// </summary>
    private static string EmitTimespan(object value)
    {
        var ts = value.ToString()!;

        // .NET TimeSpan.ToString() format: [-][d.]hh:mm:ss[.fffffff]
        if (ts.Contains(':'))
        {
            if (TimeSpan.TryParse(ts, out var parsed))
            {
                // Decompose to DuckDB-compatible parts. Each component keeps its own
                // sign: for a negative TimeSpan the .NET components are individually
                // negative, and DuckDB parses an INTERVAL string per-unit. A single
                // leading "-" would negate only the first unit, so e.g. -00:01:30
                // would become "-1 minutes 30 seconds" = -30s instead of -90s.
                var parts = new List<string>();
                if (parsed.Days != 0)
                {
                    parts.Add($"{parsed.Days} days");
                }

                if (parsed.Hours != 0)
                {
                    parts.Add($"{parsed.Hours} hours");
                }

                if (parsed.Minutes != 0)
                {
                    parts.Add($"{parsed.Minutes} minutes");
                }

                if (parsed.Seconds != 0)
                {
                    parts.Add($"{parsed.Seconds} seconds");
                }

                if (parsed.Milliseconds != 0)
                {
                    parts.Add($"{parsed.Milliseconds} milliseconds");
                }

                if (parts.Count == 0)
                {
                    parts.Add("0 seconds");
                }

                return $"INTERVAL '{string.Join(" ", parts)}'";
            }
        }

        // KQL shorthand: 7d, 2h, 30m, 10s, 500ms
        if (ts.EndsWith("ms"))
        {
            return $"INTERVAL '{ts[..^2]} milliseconds'";
        }

        if (ts.EndsWith("us"))
        {
            return $"INTERVAL '{ts[..^2]} microseconds'";
        }

        if (ts.EndsWith("d"))
        {
            return $"INTERVAL '{ts[..^1]} days'";
        }

        if (ts.EndsWith("h"))
        {
            return $"INTERVAL '{ts[..^1]} hours'";
        }

        if (ts.EndsWith("m"))
        {
            return $"INTERVAL '{ts[..^1]} minutes'";
        }

        if (ts.EndsWith("s"))
        {
            return $"INTERVAL '{ts[..^1]} seconds'";
        }

        return $"INTERVAL '{ts}'";
    }

    #endregion Timespan

    #region Helpers

    /// <summary>
    /// Wraps a LIKE pattern operand so that %, _, and \ in the search term
    /// are treated as literals, not wildcards. For string literals, escaping
    /// is done at SQL generation time. For column refs, we emit a runtime
    /// replace chain.
    /// KQL substring operators always treat the search text as literal text.
    /// </summary>
    private static string EscapeLikePattern(ScalarExpr operand, string emitted)
    {
        if (operand is LiteralScalar { Kind: LiteralKind.String, Value: string s })
        {
            // Escape at code generation time: \ → \\, % → \%, _ → \_
            var escaped = s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            return $"'{DuckDbSqlText.EscapeString(escaped)}'";
        }
        // For dynamic/column operands: emit a runtime escape chain
        return $"replace(replace(replace({emitted}, '\\', '\\\\'), '%', '\\%'), '_', '\\_')";
    }

    /// <summary>
    /// Wraps a regex operand to escape regex metacharacters in the search term,
    /// treating the term as literal text for has/hasprefix/hassuffix operators.
    /// Uses DuckDB's regexp_escape() when available, falls back to a chain for
    /// common metacharacters.
    /// </summary>
    private static string EscapeRegexTerm(ScalarExpr operand, string emitted)
    {
        if (operand is LiteralScalar { Kind: LiteralKind.String, Value: string s })
        {
            // Escape regex metacharacters for RE2: . * + ? ^ $ { } [ ] | ( ) \
            var escaped = Regex.Escape(s);
            return $"'{DuckDbSqlText.EscapeString(escaped)}'";
        }
        // For dynamic operands: use DuckDB's regexp_escape() function
        return $"regexp_escape({emitted})";
    }
    private static string EmitTimestampLiteral(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Utc => value,
            _ => value
        };

        return "TIMESTAMP '" +
               utc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
               "'";
    }
    #endregion Helpers
}
