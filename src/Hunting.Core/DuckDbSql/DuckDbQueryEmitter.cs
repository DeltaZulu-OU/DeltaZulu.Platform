namespace Hunting.Core.DuckDbSql;

using System.Text;
using System.Text.RegularExpressions;
using QueryModel;

/// <summary>
/// Emits DuckDB SQL from a RelNode query tree.
///
/// Canonical emission uses CTE staging (__kql_stage_N) to preserve pipeline
/// operator boundaries. Each tabular operator becomes a named CTE stage.
/// The final SELECT reads from the last stage.
///
/// SQL is transient — generated, executed, discarded.
/// </summary>
public sealed partial class DuckDbQueryEmitter
{
    private sealed record TerminalTopK(string Source, string OrderBy, int Limit);
    private sealed record TerminalOrder(string Source, string OrderBy);
    private sealed record TerminalLimit(string Source, int Limit);
    private sealed record SelectStageShape(
        string Projection,
        string Source,
        string? Predicate = null,
        string? GroupByClause = null,
        string? OrderBy = null,
        int? Limit = null);

    [GeneratedRegex(
        @"^SELECT \* FROM (main\.[A-Za-z_][A-Za-z0-9_]*)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex TrivialSourceStageRegex();

    [GeneratedRegex(
        @"^SELECT \* FROM (?<source>[A-Za-z0-9_.]+) ORDER BY (?<order>.+) LIMIT (?<limit>\d+)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex TerminalTopKRegex();

    [GeneratedRegex(
        @"^SELECT \* FROM (?<source>[A-Za-z0-9_.]+) ORDER BY (?<order>.+)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex TerminalOrderRegex();

    [GeneratedRegex(
        @"^SELECT \* FROM (?<source>.+) LIMIT (?<limit>\d+)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex TerminalLimitRegex();

    [GeneratedRegex(
        @"^SELECT (?<proj>.+) FROM (?<source>[A-Za-z0-9_.]+)(?<group>\s+GROUP BY .+)?$",
        RegexOptions.IgnoreCase)]
    private static partial Regex FinalStageInlineRegex();

    [GeneratedRegex(
        @"^SELECT \* FROM (?<source>[A-Za-z0-9_.]+) WHERE (?<pred>.+)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex FilterStageInlineRegex();

    [GeneratedRegex(@"__kql_stage_\d+")]
    private static partial Regex StageRefRegex();

    private int _stageCounter;
    private readonly List<(string Name, string Sql)> _ctes = [];
    private readonly HashSet<string> _stageNames = new(StringComparer.Ordinal);
    private readonly int _defaultLimit;
    private readonly bool _applyDefaultLimit;

    // Scalar let bindings: name → emitted SQL expression, populated by EmitLet.
    // EmitScalar checks this dictionary for ColumnRef resolution so that
    // `let cutoff = ago(7d); T | where Timestamp > cutoff` emits
    // `Timestamp > (current_timestamp - INTERVAL '7 days')` not `Timestamp > cutoff`.
    private readonly Dictionary<string, string> _scalarBindings =
        new(StringComparer.OrdinalIgnoreCase);

    public DuckDbQueryEmitter(int defaultLimit = 10_000, bool applyDefaultLimit = true)
    {
        _defaultLimit = defaultLimit;
        _applyDefaultLimit = applyDefaultLimit;
    }

    /// <summary>
    /// Emit a complete DuckDB SQL statement from a RelNode tree.
    /// </summary>
    public string Emit(RelNode node)
    {
        _stageCounter = 0;
        _ctes.Clear();
        _stageNames.Clear();
        _scalarBindings.Clear();

        var (finalSource, columns) = EmitNode(node);
        var hasUserLimit = HasLimit(node);
        var terminalTopK = ShapeSql(ref finalSource);
        var terminalOrder = TryExtractTerminalOrder(finalSource);
        if (terminalOrder is not null)
        {
            finalSource = terminalOrder.Source;
        }
        TerminalLimit? terminalLimit = null;
        TryInlineSingleUseAggregateStageIntoTerminalTopK(ref terminalTopK, ref columns);
        TryInlineSingleUseAggregateStageIntoTerminalOrder(ref terminalOrder, ref columns);
        if (terminalOrder is not null)
        {
            // Aggregate inlining into terminal ORDER may rewrite its source.
            // Keep finalSource in sync before subsequent inlining passes run.
            finalSource = terminalOrder.Source;
        }
        TryInlineSingleUseFilterStageIntoProjection(ref finalSource, ref columns);
        TryInlineFinalStage(ref finalSource, ref columns);
        // Final-stage inlining can materialize the projection list from a stage source.
        // Re-run filter inlining once so where|project|take shapes can collapse fully.
        TryInlineSingleUseFilterStageIntoProjection(ref finalSource, ref columns);
        if (terminalOrder is not null)
        {
            // Keep terminal ORDER source aligned with later projection/filter inlining.
            // Without this, we can remove an intermediate stage from _ctes and still
            // reference it in final FROM (dangling __kql_stage_N).
            terminalOrder = terminalOrder with { Source = finalSource };
        }
        if (terminalTopK is null && terminalOrder is null)
        {
            terminalLimit = TryExtractTerminalLimit(finalSource);
            if (terminalLimit is not null)
            {
                finalSource = terminalLimit.Source;
                // LIMIT extraction can expose a terminal projection stage.
                // Inline it (and then its single-use filter input) to fully
                // collapse where|project|take into one SELECT block.
                TryInlineFinalStage(ref finalSource, ref columns);
                TryInlineSingleUseFilterStageIntoProjection(ref finalSource, ref columns);
            }
        }
        if (terminalLimit is not null)
        {
            // Keep terminal LIMIT source aligned with later projection/filter inlining.
            // Without this, we can remove an intermediate stage from _ctes and still
            // reference it in final FROM (dangling __kql_stage_N).
            terminalLimit = terminalLimit with { Source = finalSource };
        }

        var sb = new StringBuilder();

        if (_ctes.Count > 0)
        {
            sb.Append("WITH ");
            for (var i = 0; i < _ctes.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(_ctes[i].Name).Append(" AS (").Append(_ctes[i].Sql).Append(')');
            }
            sb.Append(' ');
        }

        sb.Append("SELECT ");
        sb.Append(columns ?? "*");
        sb.Append(" FROM ").Append(terminalTopK?.Source ?? terminalOrder?.Source ?? terminalLimit?.Source ?? finalSource);

        if (terminalTopK is not null)
        {
            sb.Append(" ORDER BY ").Append(terminalTopK.OrderBy);
            sb.Append(" LIMIT ").Append(terminalTopK.Limit);
        }
        else if (terminalOrder is not null)
        {
            sb.Append(" ORDER BY ").Append(terminalOrder.OrderBy);
            if (!hasUserLimit && _applyDefaultLimit)
            {
                sb.Append(" LIMIT ").Append(_defaultLimit);
            }
        }
        else if (terminalLimit is not null)
        {
            sb.Append(" LIMIT ").Append(terminalLimit.Limit);
        }
        else if (!hasUserLimit && _applyDefaultLimit)
        {
            sb.Append(" LIMIT ").Append(_defaultLimit);
        }

        return sb.ToString();
    }

    private (string Source, string? Columns) EmitNode(RelNode node) => node switch
    {
        ScanNode scan => EmitScan(scan),
        FilterNode filter => EmitFilter(filter),
        ProjectNode project => EmitProject(project),
        ExtendNode extend => EmitExtend(extend),
        AggregateNode agg => EmitAggregate(agg),
        SortNode sort => EmitSort(sort),
        LimitNode limit => EmitLimit(limit),
        DistinctNode dist => EmitDistinct(dist),
        JoinNode join => EmitJoin(join),
        LetBindingNode let_ => EmitLet(let_),
        _ => throw new NotSupportedException($"Unsupported RelNode type: {node.GetType().Name}")
    };

    private string NextStage() => $"__kql_stage_{_stageCounter++}";

    private string StageFrom(RelNode input)
    {
        var (source, cols) = EmitNode(input);
        // Pass-through elimination: when the input already produced a standalone
        // CTE stage and no column projection needs to be applied, reuse that
        // stage directly instead of emitting a redundant `SELECT * FROM stage`
        // wrapper. Base-table scans (`main.X`) are never CTE references, so the
        // scan still gets its own stage.
        if (cols is null && IsStageReference(source))
        {
            return source;
        }

        var stage = NextStage();
        var sql = $"SELECT {cols ?? "*"} FROM {source}";
        _ctes.Add((stage, sql));
        _stageNames.Add(stage);
        return stage;
    }

    private bool IsStageReference(string source) => _stageNames.Contains(source);

    private TerminalTopK? ShapeSql(ref string finalSource)
    {
        var terminalTopK = TryExtractTerminalTopK(finalSource);
        var substitutions = new Dictionary<string, string>(StringComparer.Ordinal);
        var refCounts = BuildStageRefCounts();
        for (var i = _ctes.Count - 1; i >= 0; i--)
        {
            var cte = _ctes[i];
            var match = TrivialSourceStageRegex().Match(cte.Sql);
            if (!match.Success)
            {
                continue;
            }

            var usage = refCounts.TryGetValue(cte.Name, out var count) ? count : 0;
            // If a trivial source stage is referenced exactly once, we can inline it
            // into that consumer. If it is referenced zero times, it is already dead
            // after earlier shaping (e.g. terminal top-k extraction) and can be dropped.
            if (usage > 1)
            {
                continue;
            }

            substitutions[cte.Name] = match.Groups[1].Value;
            _ctes.RemoveAt(i);
            _stageNames.Remove(cte.Name);
        }

        if (substitutions.Count > 0)
        {
            for (var i = 0; i < _ctes.Count; i++)
            {
                var sql = _ctes[i].Sql;
                foreach (var pair in substitutions)
                {
                    sql = sql.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
                }

                _ctes[i] = (_ctes[i].Name, sql);
            }

            foreach (var pair in substitutions)
            {
                if (string.Equals(finalSource, pair.Key, StringComparison.Ordinal))
                {
                    finalSource = pair.Value;
                }

                if (terminalTopK is not null
                    && string.Equals(terminalTopK.Source, pair.Key, StringComparison.Ordinal))
                {
                    terminalTopK = terminalTopK with { Source = pair.Value };
                }
            }
        }

        return terminalTopK;
    }

    private Dictionary<string, int> BuildStageRefCounts()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var cte in _ctes)
        {
            foreach (Match match in StageRefRegex().Matches(cte.Sql))
            {
                var stageRef = match.Value;
                counts[stageRef] = counts.TryGetValue(stageRef, out var count) ? count + 1 : 1;
            }
        }

        return counts;
    }

    private int CountStageReferences(string stageName)
    {
        var refs = 0;
        foreach (var cte in _ctes)
        {
            if (string.Equals(cte.Name, stageName, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match match in StageRefRegex().Matches(cte.Sql))
            {
                if (string.Equals(match.Value, stageName, StringComparison.Ordinal))
                {
                    refs++;
                }
            }
        }

        return refs;
    }

    private static SelectStageShape? ParseSelectShape(string sql)
    {
        var topK = TerminalTopKRegex().Match(sql);
        if (topK.Success)
        {
            return new SelectStageShape(
                Projection: "*",
                Source: topK.Groups["source"].Value,
                OrderBy: topK.Groups["order"].Value,
                Limit: int.Parse(topK.Groups["limit"].Value));
        }

        var order = TerminalOrderRegex().Match(sql);
        if (order.Success)
        {
            return new SelectStageShape("*", order.Groups["source"].Value, OrderBy: order.Groups["order"].Value);
        }

        var filter = FilterStageInlineRegex().Match(sql);
        if (filter.Success)
        {
            return new SelectStageShape(
                Projection: "*",
                Source: filter.Groups["source"].Value,
                Predicate: filter.Groups["pred"].Value);
        }

        var final = FinalStageInlineRegex().Match(sql);
        if (final.Success)
        {
            return new SelectStageShape(
                Projection: final.Groups["proj"].Value,
                Source: final.Groups["source"].Value,
                GroupByClause: final.Groups["group"].Value);
        }

        return null;
    }


    private TerminalTopK? TryExtractTerminalTopK(string finalSource)
    {
        var idx = _ctes.FindIndex(c => string.Equals(c.Name, finalSource, StringComparison.Ordinal));
        if (idx < 0)
        {
            return null;
        }

        var terminal = _ctes[idx];
        var parsed = ParseSelectShape(terminal.Sql);
        if (parsed is null || parsed.OrderBy is null || parsed.Limit is null || parsed.Projection != "*")
        {
            return null;
        }

        _ctes.RemoveAt(idx);
        _stageNames.Remove(terminal.Name);
        return new TerminalTopK(
            parsed.Source,
            parsed.OrderBy,
            parsed.Limit.Value);
    }

    private TerminalOrder? TryExtractTerminalOrder(string finalSource)
    {
        var idx = _ctes.FindIndex(c => string.Equals(c.Name, finalSource, StringComparison.Ordinal));
        if (idx < 0)
        {
            return null;
        }

        var terminal = _ctes[idx];
        var parsed = ParseSelectShape(terminal.Sql);
        if (parsed is null || parsed.OrderBy is null || parsed.Limit is not null || parsed.Projection != "*")
        {
            return null;
        }

        _ctes.RemoveAt(idx);
        _stageNames.Remove(terminal.Name);
        return new TerminalOrder(parsed.Source, parsed.OrderBy);
    }

    private TerminalLimit? TryExtractTerminalLimit(string finalSource)
    {
        var idx = _ctes.FindIndex(c => string.Equals(c.Name, finalSource, StringComparison.Ordinal));
        if (idx < 0)
        {
            return null;
        }

        var terminal = _ctes[idx];
        var match = TerminalLimitRegex().Match(terminal.Sql);
        if (!match.Success)
        {
            return null;
        }

        _ctes.RemoveAt(idx);
        _stageNames.Remove(terminal.Name);
        return new TerminalLimit(
            match.Groups["source"].Value,
            int.Parse(match.Groups["limit"].Value));
    }

    private void TryInlineFinalStage(ref string finalSource, ref string? columns)
    {
        if (columns is not null && !string.Equals(columns, "*", StringComparison.Ordinal))
        {
            return;
        }

        var currentSource = finalSource;
        var idx = _ctes.FindIndex(c => string.Equals(c.Name, currentSource, StringComparison.Ordinal));
        finalSource = currentSource;
        if (idx < 0)
        {
            return;
        }

        var cte = _ctes[idx];
        var m = FinalStageInlineRegex().Match(cte.Sql);
        if (!m.Success)
        {
            return;
        }

        // Inline only when this final stage is not referenced by any other CTE.
        var refs = CountStageReferences(cte.Name);
        if (refs != 0)
        {
            return;
        }

        columns = m.Groups["proj"].Value;
        var source = m.Groups["source"].Value;
        var groupBy = m.Groups["group"].Value;
        finalSource = string.IsNullOrWhiteSpace(groupBy) ? source : $"{source}{groupBy}";

        _ctes.RemoveAt(idx);
        _stageNames.Remove(cte.Name);
    }

    private void TryInlineSingleUseFilterStageIntoProjection(ref string finalSource, ref string? columns)
    {
        if (columns is null || string.Equals(columns, "*", StringComparison.Ordinal))
        {
            return;
        }

        // We cannot use ref string in FindIndex, so we need to capture the index and the filter stage name separately here.
        // Do not inline currentSource.
        var currentSource = finalSource;
        var idx = _ctes.FindIndex(c => string.Equals(c.Name, currentSource, StringComparison.Ordinal));
        finalSource = currentSource;
        if (idx < 0)
        {
            return;
        }

        var cte = _ctes[idx];
        var m = FilterStageInlineRegex().Match(cte.Sql);
        if (!m.Success)
        {
            return;
        }

        var refs = CountStageReferences(cte.Name);
        if (refs != 0)
        {
            return;
        }

        var source = m.Groups["source"].Value;
        var predicate = m.Groups["pred"].Value;

        var sourceIdx = _ctes.FindIndex(c => string.Equals(c.Name, source, StringComparison.Ordinal));
        if (sourceIdx >= 0)
        {
            var sourceCte = _ctes[sourceIdx];
            var sourceFilter = FilterStageInlineRegex().Match(sourceCte.Sql);
            if (sourceFilter.Success)
            {
                var sourceRefs = CountStageReferences(sourceCte.Name);
                if (sourceRefs == 1)
                {
                    source = sourceFilter.Groups["source"].Value;
                    var sourcePredicate = sourceFilter.Groups["pred"].Value;
                    predicate = $"({sourcePredicate}) AND ({predicate})";
                    _ctes.RemoveAt(sourceIdx);
                    _stageNames.Remove(sourceCte.Name);

                    if (sourceIdx < idx)
                    {
                        idx--;
                    }
                }
            }
        }

        finalSource = $"{source} WHERE {predicate}";
        _ctes.RemoveAt(idx);
        _stageNames.Remove(cte.Name);
    }

    private void TryInlineSingleUseAggregateStageIntoTerminalTopK(
        ref TerminalTopK? terminalTopK,
        ref string? columns)
    {
        if (terminalTopK is null || (columns is not null && !string.Equals(columns, "*", StringComparison.Ordinal)))
        {
            return;
        }

        var sourceStage = terminalTopK.Source;
        var idx = _ctes.FindIndex(c => string.Equals(c.Name, sourceStage, StringComparison.Ordinal));
        if (idx < 0)
        {
            return;
        }

        var cte = _ctes[idx];
        var m = FinalStageInlineRegex().Match(cte.Sql);
        if (!m.Success)
        {
            return;
        }

        var groupBy = m.Groups["group"].Value;
        if (string.IsNullOrWhiteSpace(groupBy))
        {
            return; // rule is intentionally scoped to aggregate shapes
        }

        var refs = CountStageReferences(cte.Name);
        if (refs != 0)
        {
            return;
        }

        columns = m.Groups["proj"].Value;
        terminalTopK = terminalTopK with
        {
            Source = $"{m.Groups["source"].Value}{groupBy}"
        };

        _ctes.RemoveAt(idx);
        _stageNames.Remove(cte.Name);
    }

    private void TryInlineSingleUseAggregateStageIntoTerminalOrder(
        ref TerminalOrder? terminalOrder,
        ref string? columns)
    {
        if (terminalOrder is null || (columns is not null && !string.Equals(columns, "*", StringComparison.Ordinal)))
        {
            return;
        }

        var sourceStage = terminalOrder.Source;
        var idx = _ctes.FindIndex(c => string.Equals(c.Name, sourceStage, StringComparison.Ordinal));
        if (idx < 0)
        {
            return;
        }

        var cte = _ctes[idx];
        var m = FinalStageInlineRegex().Match(cte.Sql);
        if (!m.Success)
        {
            return;
        }

        var groupBy = m.Groups["group"].Value;
        if (string.IsNullOrWhiteSpace(groupBy))
        {
            return;
        }

        var refs = CountStageReferences(cte.Name);
        if (refs != 0)
        {
            return;
        }

        columns = m.Groups["proj"].Value;
        terminalOrder = terminalOrder with
        {
            Source = $"{m.Groups["source"].Value}{groupBy}"
        };

        _ctes.RemoveAt(idx);
        _stageNames.Remove(cte.Name);
    }

    // ─── Scan ───────────────────────────────────────────────────────

    private (string Source, string? Columns) EmitScan(ScanNode scan) => ($"main.{EscapeIdent(scan.ViewName)}", null);

    // ─── Filter ─────────────────────────────────────────────────────

    private (string Source, string? Columns) EmitFilter(FilterNode filter)
    {
        var source = StageFrom(filter.Input);
        var stage = NextStage();
        var pred = EmitScalar(filter.Predicate);
        _ctes.Add((stage, $"SELECT * FROM {source} WHERE {pred}"));
        _stageNames.Add(stage);
        return (stage, null);
    }

    // ─── Project ────────────────────────────────────────────────────

    private (string Source, string? Columns) EmitProject(ProjectNode project)
    {
        var source = StageFrom(project.Input);
        var cols = string.Join(", ", project.Projections.Select(EmitProjection));
        return (source, cols);
    }

    // ─── Extend ─────────────────────────────────────────────────────

    private (string Source, string? Columns) EmitExtend(ExtendNode extend)
    {
        var source = StageFrom(extend.Input);
        var extensions = string.Join(", ", extend.Extensions.Select(EmitProjection));
        var stage = NextStage();
        _ctes.Add((stage, $"SELECT *, {extensions} FROM {source}"));
        _stageNames.Add(stage);
        return (stage, null);
    }

    // ─── Aggregate ──────────────────────────────────────────────────

    private (string Source, string? Columns) EmitAggregate(AggregateNode agg)
    {
        var source = StageFrom(agg.Input);
        var groupCols = agg.GroupBy.Select(EmitScalar).ToList();
        var aggCols = agg.Aggregates.Select(EmitProjection).ToList();

        var allCols = new List<string>();
        allCols.AddRange(groupCols);
        allCols.AddRange(aggCols);

        var stage = NextStage();
        var sql = $"SELECT {string.Join(", ", allCols)} FROM {source}";
        if (groupCols.Count > 0)
        {
            sql += $" GROUP BY {string.Join(", ", groupCols)}";
        }

        _ctes.Add((stage, sql));
        _stageNames.Add(stage);
        return (stage, null);
    }

    // ─── Sort ───────────────────────────────────────────────────────

    private (string Source, string? Columns) EmitSort(SortNode sort)
    {
        var source = StageFrom(sort.Input);
        var orders = string.Join(", ", sort.Sorts.Select(s => EmitTabularSortExpr(s, sort.Input)));
        var stage = NextStage();
        _ctes.Add((stage, $"SELECT * FROM {source} ORDER BY {orders}"));
        _stageNames.Add(stage);
        return (stage, null);
    }

    // ─── Distinct ───────────────────────────────────────────────────

    private (string Source, string? Columns) EmitDistinct(DistinctNode dist)
    {
        var source = StageFrom(dist.Input);
        var cols = string.Join(", ", dist.Projections.Select(EmitProjection));
        var stage = NextStage();
        _ctes.Add((stage, $"SELECT DISTINCT {cols} FROM {source}"));
        _stageNames.Add(stage);
        return (stage, null);
    }

    // ─── Limit ──────────────────────────────────────────────────────

    private (string Source, string? Columns) EmitLimit(LimitNode limit)
    {
        // Sort/take fusion: a SortNode directly beneath a LimitNode is a single
        // top-k operation. Emit ORDER BY and LIMIT in one query block rather than
        // two stages — this also avoids depending on ordering surviving an
        // intermediate CTE boundary.
        if (limit.Input is SortNode sort)
        {
            var sortSource = StageFrom(sort.Input);
            var orders = string.Join(", ", sort.Sorts.Select(s => EmitTabularSortExpr(s, sort.Input)));
            var fused = NextStage();
            _ctes.Add((fused, $"SELECT * FROM {sortSource} ORDER BY {orders} LIMIT {limit.Count}"));
            _stageNames.Add(fused);
            return (fused, null);
        }

        var source = StageFrom(limit.Input);
        var stage = NextStage();
        _ctes.Add((stage, $"SELECT * FROM {source} LIMIT {limit.Count}"));
        _stageNames.Add(stage);
        return (stage, null);
    }

    // ─── Join ───────────────────────────────────────────────────────

    private (string Source, string? Columns) EmitJoin(JoinNode join)
    {
        var leftSource = StageFrom(join.Left);
        var rightSource = StageFrom(join.Right);
        var pred = EmitScalar(join.OnPredicate);

        var joinKind = join.Kind switch
        {
            JoinKind.Inner => "INNER JOIN",
            JoinKind.LeftOuter => "LEFT JOIN",
            JoinKind.LeftSemi => "SEMI JOIN",
            JoinKind.LeftAnti => "ANTI JOIN",
            _ => throw new NotSupportedException($"Unsupported join kind: {join.Kind}")
        };

        var stage = NextStage();
        _ctes.Add((stage, $"SELECT * FROM {leftSource} {joinKind} {rightSource} ON {pred}"));
        _stageNames.Add(stage);
        return (stage, null);
    }

    // ─── Let ────────────────────────────────────────────────────────

    private (string Source, string? Columns) EmitLet(LetBindingNode let_)
    {
        if (let_.ScalarValue is not null)
        {
            // Scalar let: emit the value expression and register it for substitution.
            // Any subsequent ColumnRef("name") in the body will resolve to this SQL.
            var valueExpr = EmitScalar(let_.ScalarValue);
            _scalarBindings[let_.Name] = valueExpr;
        }

        if (let_.TabularValue is not null)
        {
            // Tabular let: emit the subquery as a named CTE.
            var (tabSource, tabCols) = EmitNode(let_.TabularValue);
            var letName = EscapeIdent(let_.Name);
            _ctes.Add((letName, $"SELECT {tabCols ?? "*"} FROM {tabSource}"));
            _stageNames.Add(letName);
        }

        return EmitNode(let_.Body);
    }

    // ─── Scalar expressions ─────────────────────────────────────────

    private string EmitScalar(ScalarExpr expr) => expr switch
    {
        // Check scalar let binding before emitting as a column identifier.
        // KQL: let cutoff = ago(7d); T | where Timestamp > cutoff
        // Without substitution: WHERE Timestamp > cutoff (undefined column)
        // With substitution:    WHERE Timestamp > (current_timestamp - INTERVAL '7 days')
        ColumnRef col when _scalarBindings.TryGetValue(col.Name, out var bound) => bound,
        ColumnRef col => EscapeIdent(col.Name),
        LiteralScalar lit => EmitLiteral(lit),
        BinaryScalar bin => EmitBinary(bin),
        UnaryScalar un => EmitUnary(un),
        FunctionCall fn => EmitFunction(fn),
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
            LiteralKind.String => $"'{EscapeString(lit.Value.ToString()!)}'",
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
            .Select(EmitScalar)
            .ToArray();

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
            ScalarUnaryOp.Not => $"(NOT {operand})",
            ScalarUnaryOp.Negate => $"(-{operand})",
            _ => throw new NotSupportedException($"Unsupported unary op: {un.Op}")
        };
    }

    private string EmitFunction(FunctionCall fn)
    {
        var args = fn.Args.Select(EmitScalar).ToList();
        var name = fn.Name.ToLowerInvariant();

        return name switch
        {
            // String functions
            "tolower" => $"lower({args[0]})",
            "toupper" => $"upper({args[0]})",
            "strlen" => $"length({args[0]})",
            "strcat" => $"concat({string.Join(", ", args)})",
            "strcat_delim" => $"concat_ws({string.Join(", ", args)})",
            "substring" => $"substring({args[0]}, ({args[1]}) + 1, {args[2]})",
            "replace_string" => $"replace({args[0]}, {args[1]}, {args[2]})",
            "replace_regex" => $"regexp_replace({args[0]}, {args[1]}, {args[2]}, 'g')",
            "split" => $"string_split({args[0]}, {args[1]})",
            "trim" => $"regexp_replace(regexp_replace({args[1]}, concat('^(', {args[0]}, ')'), ''), concat('(', {args[0]}, ')$'), '')",
            "extract" => $"COALESCE(regexp_extract({args[2]}, {args[0]}, CAST({args[1]} AS INTEGER)), '')",

            // DateTime functions
            "ago" => EmitAgo(args),
            "now" => "current_timestamp",
            "bin" when args.Count >= 2 => $"time_bucket({EmitTimespanArg(fn.Args, 1)}, {args[0]})",
            "bin_at" => $"time_bucket({EmitTimespanArg(fn.Args, 1)}, {args[0]}, {args[2]})",
            "startofday" => $"date_trunc('day', {args[0]})",
            "startofmonth" => $"date_trunc('month', {args[0]})",
            "startofweek" => $"date_trunc('week', {args[0]})",
            "startofyear" => $"date_trunc('year', {args[0]})",
            "endofday" => $"(date_trunc('day', {args[0]}) + INTERVAL '1 day' - INTERVAL '1 microsecond')",
            "endofmonth" => $"(last_day({args[0]})::TIMESTAMP + INTERVAL '23 hours 59 minutes 59 seconds 999999 microseconds')",
            "endofweek" => $"(date_trunc('week', {args[0]}) + INTERVAL '7 days' - INTERVAL '1 microsecond')",
            "endofyear" => $"(date_trunc('year', {args[0]}) + INTERVAL '1 year' - INTERVAL '1 microsecond')",
            // datetime_diff(part, dt1, dt2) → date_diff(part, dt2, dt1)
            // Spec §9.9: KQL returns dt1 - dt2 periods; DuckDB date_diff(part, start, end)
            // = end - start. So to preserve sign: date_diff(part, dt2, dt1)
            "datetime_diff" => $"date_diff({args[0]}, {args[2]}, {args[1]})",
            "datetime_add" => EmitDatetimeAdd(fn.Args, args),
            "datetime_part" => $"date_part({args[0]}, {args[1]})",
            "dayofweek" => $"date_part('dow', {args[0]})",
            "dayofmonth" => $"date_part('day', {args[0]})",
            "dayofyear" => $"date_part('doy', {args[0]})",
            "monthofyear" or "getmonth" => $"date_part('month', {args[0]})",
            "getyear" => $"date_part('year', {args[0]})",
            "hourofday" => $"date_part('hour', {args[0]})",
            "unixtime_seconds_todatetime" => $"to_timestamp({args[0]})",
            "unixtime_milliseconds_todatetime" => $"epoch_ms(CAST({args[0]} AS BIGINT))",
            "unixtime_microseconds_todatetime" => $"make_timestamp({args[0]})",
            "unixtime_nanoseconds_todatetime" => $"make_timestamp_ns({args[0]})",
            "make_datetime" => $"make_timestamp({string.Join(", ", args)})",
            "todatetime" => $"CAST({args[0]} AS TIMESTAMP)",

            // Type conversion
            "tostring" => $"CAST({args[0]} AS VARCHAR)",
            "tolong" => $"CAST({args[0]} AS BIGINT)",
            "toint" => $"CAST({args[0]} AS INTEGER)",
            "todouble" or "toreal" => $"CAST({args[0]} AS DOUBLE)",
            "tobool" => $"CAST({args[0]} AS BOOLEAN)",

            // Conditional
            "iff" or "iif" => $"CASE WHEN {args[0]} THEN {args[1]} ELSE {args[2]} END",
            "coalesce" => $"COALESCE({string.Join(", ", args)})",

            // Null tests
            "isnull" => $"({args[0]} IS NULL)",
            "isnotnull" => $"({args[0]} IS NOT NULL)",
            "isempty" => $"({args[0]} IS NULL OR {args[0]} = '')",
            "isnotempty" => $"({args[0]} IS NOT NULL AND {args[0]} != '')",

            // JSON
            "parse_json" => $"CAST({args[0]} AS JSON)",

            // Math
            "abs" => $"abs({args[0]})",

            // Aggregation (when used inside AggregateNode projections)
            "count" => args.Count == 0 ? "count(*)" : $"count({args[0]})",
            "countif" => $"count(*) FILTER (WHERE {args[0]})",
            "sum" => $"sum({args[0]})",
            "sumif" => $"sum({args[0]}) FILTER (WHERE {args[1]})",
            "avg" => $"avg({args[0]})",
            "avgif" => $"avg({args[0]}) FILTER (WHERE {args[1]})",
            "min" => $"min({args[0]})",
            "max" => $"max({args[0]})",
            "dcount" => $"count(DISTINCT {args[0]})",
            "dcountif" => $"count(DISTINCT {args[0]}) FILTER (WHERE {args[1]})",
            "arg_min" => $"arg_min({string.Join(", ", args)})",
            "arg_max" => $"arg_max({string.Join(", ", args)})",
            "make_set" when args.Count == 1 => $"list(DISTINCT {args[0]})",
            "make_set" => $"list_slice(list(DISTINCT {args[0]}), 1, {args[1]})",
            "make_list" when args.Count == 1 => $"list({args[0]})",
            "make_list" => $"list_slice(list({args[0]}), 1, {args[1]})",

            // Unknown function: reject rather than emit raw SQL.
            // Emitting an unknown name violates the project safety rule.
            _ => throw new NotSupportedException(
                $"KQL function '{name}' is not in the supported function allowlist. " +
                "Add an explicit mapping or reject it.")
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
            escapedCi = $"'{EscapeString(escaped)}'";
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

    /// DuckDB official docs do NOT list ago() as a function.
    /// The documented pattern is current_timestamp - INTERVAL.
    /// Source: https://duckdb.org/docs/current/sql/functions/timestamp
    /// </summary>
    private static string EmitAgo(List<string> args) =>
        // args[0] is already the emitted INTERVAL literal (e.g., INTERVAL '7 days')
        $"(current_timestamp - {args[0]})";

    /// <summary>
    /// Emit datetime_add(part, amount, datetime).
    /// KQL: datetime_add('hour', 3, ts) → ts + INTERVAL '3 hours'
    /// We extract the part name from the literal argument to build a valid INTERVAL.
    /// </summary>
    private static string EmitDatetimeAdd(IReadOnlyList<ScalarExpr> rawArgs, List<string> args)
    {
        // Try to extract the part name from the first argument (should be a string literal)
        var unit = "seconds"; // fallback
        if (rawArgs.Count >= 1 && rawArgs[0] is LiteralScalar { Value: string partName })
        {
            unit = partName.ToLowerInvariant() switch
            {
                "year" => "years",
                "quarter" => "months", // 3 months — multiply below
                "month" => "months",
                "week" => "weeks",
                "day" => "days",
                "hour" => "hours",
                "minute" => "minutes",
                "second" => "seconds",
                "millisecond" => "milliseconds",
                "microsecond" => "microseconds",
                _ => partName + "s"
            };

            var multiplier = partName.ToLowerInvariant() == "quarter" ? $"(({args[1]}) * 3)" : args[1];
            return $"({args[2]} + ({multiplier}) * INTERVAL '1 {unit}')";
        }

        // Non-literal part: fall back to CAST-based approach
        return $"({args[2]} + CAST(CAST({args[1]} AS VARCHAR) || ' ' || REPLACE({args[0]}, '''', '') AS INTERVAL))";
    }

    /// <summary>
    /// Helper for bin/bin_at: if the argument is a timespan literal,
    /// emit it as INTERVAL directly rather than relying on the generic
    /// EmitScalar which may have already formatted it.
    /// </summary>
    private string EmitTimespanArg(IReadOnlyList<ScalarExpr> args, int index) => EmitScalar(args[index]);

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

    // ─── Window expressions ─────────────────────────────────────────

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
            sb.Append(string.Join(", ", win.Window.PartitionBy.Select(EmitScalar)));
            if (win.Window.OrderBy.Count > 0)
            {
                sb.Append(' ');
            }
        }

        if (win.Window.OrderBy.Count > 0)
        {
            sb.Append("ORDER BY ");
            sb.Append(string.Join(", ", win.Window.OrderBy.Select(EmitSortExpr)));
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

    // ─── Sort expression ────────────────────────────────────────────

    /// <summary>
    /// Tabular ORDER BY term. Mirrors <see cref="EmitSortExpr"/> but drops the
    /// NULLS modifier when the analyst did not request one and the sort key is
    /// provably non-nullable — null ordering is then unobservable, so the
    /// explicit modifier is noise. Window ORDER BY clauses keep explicit NULLS
    /// ordering via <see cref="EmitSortExpr"/>.
    /// </summary>
    private string EmitTabularSortExpr(SortExpr sort, RelNode sortInput)
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

    // ─── Projection ─────────────────────────────────────────────────

    private string EmitProjection(ProjectionExpr proj)
    {
        var expr = EmitScalar(proj.Expression);
        if (proj.Expression is ColumnRef col && col.Name == proj.Alias)
        {
            return expr; // no alias needed
        }

        return $"{expr} AS {EscapeIdent(proj.Alias)}";
    }

    // ─── Timespan ───────────────────────────────────────────────────

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
                // Decompose to DuckDB-compatible parts
                var parts = new List<string>();
                if (parsed.Days != 0)
                {
                    parts.Add($"{Math.Abs(parsed.Days)} days");
                }

                if (parsed.Hours != 0)
                {
                    parts.Add($"{Math.Abs(parsed.Hours)} hours");
                }

                if (parsed.Minutes != 0)
                {
                    parts.Add($"{Math.Abs(parsed.Minutes)} minutes");
                }

                if (parsed.Seconds != 0)
                {
                    parts.Add($"{Math.Abs(parsed.Seconds)} seconds");
                }

                if (parsed.Milliseconds != 0)
                {
                    parts.Add($"{Math.Abs(parsed.Milliseconds)} milliseconds");
                }

                if (parts.Count == 0)
                {
                    parts.Add("0 seconds");
                }

                var sign = parsed < TimeSpan.Zero ? "-" : "";
                return $"INTERVAL '{sign}{string.Join(" ", parts)}'";
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

    // ─── Helpers ────────────────────────────────────────────────────

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
            return $"'{EscapeString(escaped)}'";
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
            return $"'{EscapeString(escaped)}'";
        }
        // For dynamic operands: use DuckDB's regexp_escape() function
        return $"regexp_escape({emitted})";
    }

    /// <summary>
    /// Returns true only when the ROOT output of the plan is bounded by a LimitNode.
    /// A limit inside a join branch does not bound the final output — the join
    /// may multiply rows — so it must NOT suppress the safety cap.
    /// </summary>
    private static bool HasLimit(RelNode node) => node switch
    {
        LimitNode => true,
        // Transparent pass-through nodes: the root limit still propagates
        FilterNode f => HasLimit(f.Input),
        ProjectNode p => HasLimit(p.Input),
        ExtendNode e => HasLimit(e.Input),
        AggregateNode => false,   // aggregate output is unbounded; cap it
        SortNode s => HasLimit(s.Input),
        JoinNode => false,        // join output is unbounded regardless of branch limits
        LetBindingNode l => HasLimit(l.Body),
        _ => false
    };

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

    private static string EscapeString(string s) => s.Replace("'", "''");
}