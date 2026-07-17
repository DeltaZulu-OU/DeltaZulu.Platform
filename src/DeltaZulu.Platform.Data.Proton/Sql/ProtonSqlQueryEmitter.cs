using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DeltaZulu.Platform.Domain.Analytics.Compilation;
using DeltaZulu.Platform.Domain.Analytics.QueryModel;

namespace DeltaZulu.Platform.Data.Proton.Sql;

/// <summary>
/// Emits Timeplus Proton-compatible SQL from a RelNode query tree.
/// Targets streaming semantics: no default row limit, schema-less table references,
/// and ClickHouse-dialect function names.
/// </summary>
public sealed class ProtonSqlQueryEmitter : IRelationalQueryEmitter
{
    public string TargetDialect => "proton";

    public EmittedQuery Emit(RelNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var sql = new ProtonEmitter().Emit(node);
        return new EmittedQuery(sql, TargetDialect);
    }

    // -------------------------------------------------------------------------
    // Internal emitter — all state is per-Emit() call
    // -------------------------------------------------------------------------

    private sealed class ProtonEmitter
    {
        private readonly List<(string Name, string Sql)> _ctes = [];
        private int _stageCounter;
        private readonly Dictionary<string, string> _scalarBindings = new(StringComparer.OrdinalIgnoreCase);
        private string? _joinLeftAlias;
        private string? _joinRightAlias;
        private bool _inAggregateProjection;

        internal string Emit(RelNode node)
        {
            if (TryEmitOptimized(node, out var optimizedSql))
            {
                return optimizedSql;
            }

            var (source, columns) = EmitNode(node);
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

            sb.Append("SELECT ").Append(columns ?? "*")
              .Append(" FROM ").Append(source);

            return sb.ToString();
        }

        private bool TryEmitOptimized(RelNode node, out string sql)
        {
            if (TryBuildSimpleSelect(node, out var shape))
            {
                sql = shape.ToSql();
                return true;
            }

            if (node is AggregateNode aggregate && TryBuildSimpleSelect(aggregate.Input, out var inputShape))
            {
                var groupCols = aggregate.GroupBy.Select(EmitScalar).ToList();

                _inAggregateProjection = true;
                List<string> aggregateCols;
                try { aggregateCols = aggregate.Aggregates.Select(EmitProjection).ToList(); }
                finally { _inAggregateProjection = false; }

                var columns = string.Join(", ", groupCols.Concat(aggregateCols));
                var sb = new StringBuilder("SELECT ").Append(columns).Append(" FROM ").Append(inputShape.Source);
                if (inputShape.Where.Count > 0)
                {
                    sb.Append(" WHERE ").AppendJoin(" AND ", inputShape.Where);
                }

                if (groupCols.Count > 0)
                {
                    sb.Append(" GROUP BY ").AppendJoin(", ", groupCols);
                }

                sql = sb.ToString();
                return true;
            }

            sql = string.Empty;
            return false;
        }

        private bool TryBuildSimpleSelect(RelNode node, out SelectShape shape)
        {
            switch (node)
            {
                case ScanNode scan:
                    shape = new SelectShape(QuoteIdent(scan.ViewName));
                    return true;

                case FilterNode filter when TryBuildSimpleSelect(filter.Input, out var input) && input.Columns is null:
                    input.Where.Add(EmitScalar(filter.Predicate));
                    shape = input;
                    return true;

                case ProjectNode project when TryBuildSimpleSelect(project.Input, out var input):
                    input.Columns = project.Projections.Select(EmitProjection).ToList();
                    shape = input;
                    return true;

                case SortNode sort when TryBuildSimpleSelect(sort.Input, out var input):
                    input.OrderBy = sort.Sorts.Select(EmitSortExpr).ToList();
                    shape = input;
                    return true;

                case LimitNode limit when TryBuildSimpleSelect(limit.Input, out var input):
                    input.Limit = limit.Count;
                    shape = input;
                    return true;

                case SampleNode sample when TryBuildSimpleSelect(sample.Input, out var input):
                    input.Limit = sample.Count;
                    shape = input;
                    return true;

                default:
                    shape = default!;
                    return false;
            }
        }

        private sealed class SelectShape(string source)
        {
            public string Source { get; } = source;
            public List<string>? Columns { get; set; }
            public List<string> Where { get; } = [];
            public List<string>? OrderBy { get; set; }
            public int? Limit { get; set; }

            public string ToSql()
            {
                var sb = new StringBuilder("SELECT ").Append(Columns is { Count: > 0 } ? string.Join(", ", Columns) : "*")
                    .Append(" FROM ").Append(Source);
                if (Where.Count > 0)
                {
                    sb.Append(" WHERE ").AppendJoin(" AND ", Where);
                }

                if (OrderBy is { Count: > 0 })
                {
                    sb.Append(" ORDER BY ").AppendJoin(", ", OrderBy);
                }

                if (Limit is { } limit)
                {
                    sb.Append(" LIMIT ").Append(limit);
                }

                return sb.ToString();
            }
        }

        private string NextStage()
        {
            var name = $"__nrt_stage_{_stageCounter++}";
            return name;
        }

        private void AddCte(string name, string sql) => _ctes.Add((name, sql));

        // ------------------------------------------------------------------
        // RelNode dispatch
        // ------------------------------------------------------------------

        private (string Source, string? Columns) EmitNode(RelNode node) => node switch {
            ScanNode scan => EmitScan(scan),
            FilterNode filter => EmitFilter(filter),
            ProjectNode project => EmitProject(project),
            ExtendNode extend => EmitExtend(extend),
            AggregateNode agg => EmitAggregate(agg),
            SortNode sort => EmitSort(sort),
            LimitNode limit => EmitLimit(limit),
            SampleNode sample => EmitSample(sample),
            DistinctNode dist => EmitDistinct(dist),
            JoinNode join => EmitJoin(join),
            SingletonRowNode => EmitSingleton(),
            LetBindingNode let_ => EmitLet(let_),
            _ => throw new NotSupportedException($"Unsupported RelNode: {node.GetType().Name}")
        };

        private string StageFrom(RelNode node)
        {
            var (source, columns) = EmitNode(node);
            if (columns is null)
            {
                return source;
            }

            var stage = NextStage();
            AddCte(stage, $"SELECT {columns} FROM {source}");
            return stage;
        }

        // ------------------------------------------------------------------
        // Scan: Proton stream references use plain names (no schema prefix)
        // ------------------------------------------------------------------

        private static (string Source, string? Columns) EmitScan(ScanNode scan) =>
            (QuoteIdent(scan.ViewName), null);

        // ------------------------------------------------------------------
        // Filter → WHERE
        // ------------------------------------------------------------------

        private (string Source, string? Columns) EmitFilter(FilterNode filter)
        {
            var source = StageFrom(filter.Input);
            var pred = EmitScalar(filter.Predicate);
            var stage = NextStage();
            AddCte(stage, $"SELECT * FROM {source} WHERE {pred}");
            return (stage, null);
        }

        // ------------------------------------------------------------------
        // Project → SELECT cols
        // ------------------------------------------------------------------

        private (string Source, string? Columns) EmitProject(ProjectNode project)
        {
            var source = StageFrom(project.Input);
            var cols = string.Join(", ", project.Projections.Select(EmitProjection));
            return (source, cols);
        }

        // ------------------------------------------------------------------
        // Extend → SELECT *, new_cols
        // ------------------------------------------------------------------

        private (string Source, string? Columns) EmitExtend(ExtendNode extend)
        {
            var extensions = string.Join(", ", extend.Extensions.Select(EmitProjection));
            if (extend.Input is FilterNode filter)
            {
                var (filterSource, filterCols) = EmitNode(filter.Input);
                var src = filterCols is null ? filterSource : $"(SELECT {filterCols} FROM {filterSource})";
                var pred = EmitScalar(filter.Predicate);
                var next = NextStage();
                AddCte(next, $"SELECT *, {extensions} FROM {src} WHERE {pred}");
                return (next, null);
            }

            var source = StageFrom(extend.Input);
            var stage = NextStage();
            AddCte(stage, $"SELECT *, {extensions} FROM {source}");
            return (stage, null);
        }

        // ------------------------------------------------------------------
        // Aggregate → SELECT + GROUP BY
        // ------------------------------------------------------------------

        private (string Source, string? Columns) EmitAggregate(AggregateNode agg)
        {
            var source = StageFrom(agg.Input);
            var groupCols = agg.GroupBy.Select(EmitScalar).ToList();

            _inAggregateProjection = true;
            List<string> aggCols;
            try { aggCols = agg.Aggregates.Select(EmitProjection).ToList(); }
            finally { _inAggregateProjection = false; }

            var allCols = groupCols.Concat(aggCols);
            var stage = NextStage();
            var sql = $"SELECT {string.Join(", ", allCols)} FROM {source}";
            if (groupCols.Count > 0)
            {
                sql += $" GROUP BY {string.Join(", ", groupCols)}";
            }

            AddCte(stage, sql);
            return (stage, null);
        }

        // ------------------------------------------------------------------
        // Sort → ORDER BY
        // ------------------------------------------------------------------

        private (string Source, string? Columns) EmitSort(SortNode sort)
        {
            var source = StageFrom(sort.Input);
            var orders = string.Join(", ", sort.Sorts.Select(EmitSortExpr));
            var stage = NextStage();
            AddCte(stage, $"SELECT * FROM {source} ORDER BY {orders}");
            return (stage, null);
        }

        // ------------------------------------------------------------------
        // Limit — skip in MV context; keep for explicit KQL take/top
        // ------------------------------------------------------------------

        private (string Source, string? Columns) EmitLimit(LimitNode limit)
        {
            if (limit.Input is SortNode sort)
            {
                var sortSource = StageFrom(sort.Input);
                var orders = string.Join(", ", sort.Sorts.Select(EmitSortExpr));
                var fused = NextStage();
                AddCte(fused, $"SELECT * FROM {sortSource} ORDER BY {orders} LIMIT {limit.Count}");
                return (fused, null);
            }

            var source = StageFrom(limit.Input);
            var stage = NextStage();
            AddCte(stage, $"SELECT * FROM {source} LIMIT {limit.Count}");
            return (stage, null);
        }

        // ------------------------------------------------------------------
        // Sample — Proton: no direct equivalent; approximate with LIMIT
        // ------------------------------------------------------------------

        private (string Source, string? Columns) EmitSample(SampleNode sample)
        {
            var source = StageFrom(sample.Input);
            var stage = NextStage();
            AddCte(stage, $"SELECT * FROM {source} LIMIT {sample.Count}");
            return (stage, null);
        }

        // ------------------------------------------------------------------
        // Distinct → SELECT DISTINCT
        // ------------------------------------------------------------------

        private (string Source, string? Columns) EmitDistinct(DistinctNode dist)
        {
            var source = StageFrom(dist.Input);
            var cols = string.Join(", ", dist.Projections.Select(EmitProjection));
            var stage = NextStage();
            AddCte(stage, $"SELECT DISTINCT {cols} FROM {source}");
            return (stage, null);
        }

        // ------------------------------------------------------------------
        // Join
        // ------------------------------------------------------------------

        private (string Source, string? Columns) EmitJoin(JoinNode join)
        {
            var (leftSource, leftCols) = EmitNode(join.Left);
            var (rightSource, rightCols) = EmitNode(join.Right);

            const string leftAlias = "_left";
            const string rightAlias = "_right";

            var leftSql = leftCols is null ? leftSource : $"(SELECT {leftCols} FROM {leftSource})";
            var rightSql = rightCols is null ? rightSource : $"(SELECT {rightCols} FROM {rightSource})";

            var prevLeft = _joinLeftAlias;
            var prevRight = _joinRightAlias;
            _joinLeftAlias = leftAlias;
            _joinRightAlias = rightAlias;
            var onPred = EmitScalar(join.OnPredicate);
            _joinLeftAlias = prevLeft;
            _joinRightAlias = prevRight;

            var joinType = join.Kind switch {
                JoinKind.Inner => "INNER JOIN",
                JoinKind.LeftOuter => "LEFT JOIN",
                JoinKind.RightOuter => "RIGHT JOIN",
                JoinKind.FullOuter => "FULL OUTER JOIN",
                JoinKind.LeftSemi => "LEFT SEMI JOIN",
                JoinKind.LeftAnti => "LEFT ANTI JOIN",
                JoinKind.RightSemi => "RIGHT SEMI JOIN",
                JoinKind.RightAnti => "RIGHT ANTI JOIN",
                _ => "INNER JOIN"
            };

            var stage = NextStage();
            AddCte(stage, $"SELECT {leftAlias}.*, {rightAlias}.* FROM {leftSql} AS {leftAlias} {joinType} {rightSql} AS {rightAlias} ON {onPred}");
            return (stage, null);
        }

        // ------------------------------------------------------------------
        // Singleton
        // ------------------------------------------------------------------

        private (string Source, string? Columns) EmitSingleton()
        {
            var stage = NextStage();
            AddCte(stage, "SELECT 1 AS __seed");
            return (stage, null);
        }

        // ------------------------------------------------------------------
        // Let bindings
        // ------------------------------------------------------------------

        private (string Source, string? Columns) EmitLet(LetBindingNode let_)
        {
            if (let_.ScalarValue is not null)
            {
                _scalarBindings[let_.Name] = EmitScalar(let_.ScalarValue);
            }

            if (let_.TabularValue is not null)
            {
                var (tabSource, tabCols) = EmitNode(let_.TabularValue);
                AddCte(QuoteIdent(let_.Name), $"SELECT {tabCols ?? "*"} FROM {tabSource}");
            }

            return EmitNode(let_.Body);
        }

        // ------------------------------------------------------------------
        // Scalar expressions
        // ------------------------------------------------------------------

        private string EmitProjection(ProjectionExpr proj)
        {
            var expr = EmitScalar(proj.Expression);
            if (proj.Expression is ColumnRef col && col.Name == proj.Alias)
            {
                return expr;
            }

            return $"{expr} AS {QuoteIdent(proj.Alias)}";
        }

        private string EmitSortExpr(SortExpr sort)
        {
            var col = EmitScalar(sort.Expression);
            var dir = sort.Direction == SortDirection.Desc ? " DESC" : " ASC";
            var nulls = sort.Nulls switch {
                NullOrder.First => " NULLS FIRST",
                NullOrder.Last => " NULLS LAST",
                _ => sort.Direction == SortDirection.Asc ? " NULLS FIRST" : " NULLS LAST"
            };
            return $"{col}{dir}{nulls}";
        }

        private string EmitScalar(ScalarExpr expr) => expr switch {
            ColumnRef { Qualifier: JoinSide.Left } col when _joinLeftAlias is not null
                => $"{_joinLeftAlias}.{QuoteIdent(col.Name)}",
            ColumnRef { Qualifier: JoinSide.Right } col when _joinRightAlias is not null
                => $"{_joinRightAlias}.{QuoteIdent(col.Name)}",
            ColumnRef col when _scalarBindings.TryGetValue(col.Name, out var bound) => bound,
            ColumnRef col => QuoteIdent(col.Name),
            LiteralScalar lit => EmitLiteral(lit),
            BinaryScalar bin => EmitBinary(bin),
            UnaryScalar un => EmitUnary(un),
            FunctionCall fn => EmitFunction(fn),
            CaseScalar cs => EmitCase(cs),
            StarExpr => "*",
            WindowScalarExpr win => EmitWindow(win),
            ListScalar list => $"({string.Join(", ", list.Items.Select(EmitScalar))})",
            _ => throw new NotSupportedException($"Unsupported ScalarExpr: {expr.GetType().Name}")
        };

        private string EmitLiteral(LiteralScalar lit)
        {
            if (lit.Value is null || lit.Kind == LiteralKind.Null)
            {
                return "NULL";
            }

            return lit.Kind switch {
                LiteralKind.String => $"'{EscapeString(lit.Value.ToString()!)}'",
                LiteralKind.Bool => Convert.ToBoolean(lit.Value, CultureInfo.InvariantCulture) ? "true" : "false",
                LiteralKind.Timespan => EmitInterval(lit.Value),
                LiteralKind.DateTime => $"TIMESTAMP '{lit.Value}'",
                _ => lit.Value.ToString()!
            };
        }

        private string EmitBinary(BinaryScalar bin)
        {
            var left = EmitScalar(bin.Left);

            if (bin.Op is ScalarBinaryOp.In or ScalarBinaryOp.NotIn)
            {
                if (bin.Right is not ListScalar list)
                {
                    throw new NotSupportedException("IN requires a list on the right side.");
                }

                var items = string.Join(", ", list.Items.Select(EmitScalar));
                var op = bin.Op == ScalarBinaryOp.NotIn ? "NOT IN" : "IN";
                return $"({left} {op} ({items}))";
            }

            var right = EmitScalar(bin.Right);

            return bin.Op switch {
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

                ScalarBinaryOp.Contains => EmitContains(bin, left, right, ci: true, negate: false),
                ScalarBinaryOp.NotContains => EmitContains(bin, left, right, ci: true, negate: true),
                ScalarBinaryOp.ContainsCs => EmitContains(bin, left, right, ci: false, negate: false),
                ScalarBinaryOp.NotContainsCs => EmitContains(bin, left, right, ci: false, negate: true),

                ScalarBinaryOp.StartsWith => EmitStartsWith(bin, left, right, ci: true, negate: false),
                ScalarBinaryOp.NotStartsWith => EmitStartsWith(bin, left, right, ci: true, negate: true),
                ScalarBinaryOp.StartsWithCs => EmitStartsWith(bin, left, right, ci: false, negate: false),
                ScalarBinaryOp.NotStartsWithCs => EmitStartsWith(bin, left, right, ci: false, negate: true),

                ScalarBinaryOp.EndsWith => EmitEndsWith(bin, left, right, ci: true, negate: false),
                ScalarBinaryOp.NotEndsWith => EmitEndsWith(bin, left, right, ci: true, negate: true),
                ScalarBinaryOp.EndsWithCs => EmitEndsWith(bin, left, right, ci: false, negate: false),
                ScalarBinaryOp.NotEndsWithCs => EmitEndsWith(bin, left, right, ci: false, negate: true),

                ScalarBinaryOp.Has => EmitHas(bin, left, ci: true, negate: false),
                ScalarBinaryOp.NotHas => EmitHas(bin, left, ci: true, negate: true),
                ScalarBinaryOp.HasCs => EmitHas(bin, left, ci: false, negate: false),
                ScalarBinaryOp.NotHasCs => EmitHas(bin, left, ci: false, negate: true),

                ScalarBinaryOp.HasPrefix => EmitHasPrefix(bin, left, ci: true, negate: false),
                ScalarBinaryOp.NotHasPrefix => EmitHasPrefix(bin, left, ci: true, negate: true),
                ScalarBinaryOp.HasPrefixCs => EmitHasPrefix(bin, left, ci: false, negate: false),
                ScalarBinaryOp.NotHasPrefixCs => EmitHasPrefix(bin, left, ci: false, negate: true),

                ScalarBinaryOp.HasSuffix => EmitHasSuffix(bin, left, ci: true, negate: false),
                ScalarBinaryOp.NotHasSuffix => EmitHasSuffix(bin, left, ci: true, negate: true),
                ScalarBinaryOp.HasSuffixCs => EmitHasSuffix(bin, left, ci: false, negate: false),
                ScalarBinaryOp.NotHasSuffixCs => EmitHasSuffix(bin, left, ci: false, negate: true),

                ScalarBinaryOp.MatchesRegex => $"match({left}, {right})",
                ScalarBinaryOp.NotMatchesRegex => $"(NOT match({left}, {right}))",

                _ => throw new NotSupportedException($"Unsupported binary op: {bin.Op}")
            };
        }

        private string EmitContains(BinaryScalar bin, string left, string right, bool ci, bool negate)
        {
            var (l, r) = CiArgs(bin.Right, left, right, ci);
            var expr = $"(position({l}, {r}) > 0)";
            return negate ? $"(NOT {expr})" : expr;
        }

        private string EmitStartsWith(BinaryScalar bin, string left, string right, bool ci, bool negate)
        {
            var (l, r) = CiArgs(bin.Right, left, right, ci);
            var expr = $"startsWith({l}, {r})";
            return negate ? $"(NOT {expr})" : $"({expr})";
        }

        private string EmitEndsWith(BinaryScalar bin, string left, string right, bool ci, bool negate)
        {
            var (l, r) = CiArgs(bin.Right, left, right, ci);
            var expr = $"endsWith({l}, {r})";
            return negate ? $"(NOT {expr})" : $"({expr})";
        }

        private string EmitHas(BinaryScalar bin, string left, bool ci, bool negate)
        {
            var rhsSql = EmitScalar(bin.Right);
            var pattern = BuildWordBoundaryPattern(bin.Right, rhsSql, ci);
            var haystack = ci ? $"lower({left})" : left;
            var expr = $"match({haystack}, {pattern})";
            return negate ? $"(NOT {expr})" : $"({expr})";
        }

        private string EmitHasPrefix(BinaryScalar bin, string left, bool ci, bool negate)
        {
            var rhsSql = EmitScalar(bin.Right);
            var pattern = BuildWordStartPattern(bin.Right, rhsSql, ci);
            var haystack = ci ? $"lower({left})" : left;
            var expr = $"match({haystack}, {pattern})";
            return negate ? $"(NOT {expr})" : $"({expr})";
        }

        private string EmitHasSuffix(BinaryScalar bin, string left, bool ci, bool negate)
        {
            var rhsSql = EmitScalar(bin.Right);
            var pattern = BuildWordEndPattern(bin.Right, rhsSql, ci);
            var haystack = ci ? $"lower({left})" : left;
            var expr = $"match({haystack}, {pattern})";
            return negate ? $"(NOT {expr})" : $"({expr})";
        }

        private (string L, string R) CiArgs(ScalarExpr rhsExpr, string left, string right, bool ci)
        {
            if (!ci)
            {
                return (left, right);
            }

            if (rhsExpr is LiteralScalar { Kind: LiteralKind.String, Value: string s })
            {
                return ($"lower({left})", $"'{EscapeString(s.ToLowerInvariant())}'");
            }

            return ($"lower({left})", $"lower({right})");
        }

        private static string BuildWordBoundaryPattern(ScalarExpr rhs, string rhsSql, bool ci) =>
            BuildWordPattern(rhs, rhsSql, ci, "(^|[^[:alnum:]])", "([^[:alnum:]]|$)", escapeRegex: true);

        private static string BuildWordStartPattern(ScalarExpr rhs, string rhsSql, bool ci) =>
            BuildWordPattern(rhs, rhsSql, ci, "(^|[^[:alnum:]])", null, escapeRegex: true);

        private static string BuildWordEndPattern(ScalarExpr rhs, string rhsSql, bool ci) =>
            BuildWordPattern(rhs, rhsSql, ci, null, "([^[:alnum:]]|$)", escapeRegex: true);

        private static string BuildWordPattern(ScalarExpr rhs, string rhsSql, bool ci,
            string? prefix, string? suffix, bool escapeRegex)
        {
            if (rhs is LiteralScalar { Kind: LiteralKind.String, Value: string s })
            {
                var term = ci ? s.ToLowerInvariant() : s;
                return $"'{prefix}{EscapeString(Regex.Escape(term))}{suffix}'";
            }
            var raw = escapeRegex
                ? $"regexp_replace(toString({rhsSql}), '([\\[\\](){{}}^$*+?.|\\\\])', '\\\\$1', 'g')"
                : $"toString({rhsSql})";
            var inner = ci ? $"lower({raw})" : raw;
            var parts = new List<string>();
            if (prefix is not null)
            {
                parts.Add($"'{prefix}'");
            }

            parts.Add(inner);
            if (suffix is not null)
            {
                parts.Add($"'{suffix}'");
            }

            return $"concat({string.Join(", ", parts)})";
        }

        private string EmitUnary(UnaryScalar un)
        {
            var operand = EmitScalar(un.Operand);
            return un.Op switch {
                ScalarUnaryOp.Not => $"NOT ({operand})",
                ScalarUnaryOp.Negate => $"(-{operand})",
                _ => throw new NotSupportedException($"Unsupported unary op: {un.Op}")
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

        private string EmitWindow(WindowScalarExpr win)
        {
            var args = win.Args.Select(EmitScalar).ToList();
            var fnCall = win.FunctionName switch {
                "count" when args.Count == 0 => "count()",
                _ when args.Count == 0 => $"{win.FunctionName}()",
                _ => $"{win.FunctionName}({string.Join(", ", args)})"
            };

            var sb = new StringBuilder(fnCall).Append(" OVER (");

            if (win.Window.PartitionBy.Count > 0)
            {
                sb.Append("PARTITION BY ").AppendJoin(", ", win.Window.PartitionBy.Select(EmitScalar));
                if (win.Window.OrderBy.Count > 0)
                {
                    sb.Append(' ');
                }
            }

            if (win.Window.OrderBy.Count > 0)
            {
                sb.Append("ORDER BY ").AppendJoin(", ", win.Window.OrderBy.Select(EmitSortExpr));
            }

            if (win.Window.Frame is { } frame)
            {
                sb.Append(' ').Append(frame.Type == WindowFrameType.Rows ? "ROWS" : "RANGE")
                  .Append(" BETWEEN ").Append(EmitWindowBound(frame.Start))
                  .Append(" AND ").Append(EmitWindowBound(frame.End));
            }

            sb.Append(')');
            return sb.ToString();
        }

        private string EmitWindowBound(WindowBound b) => b.Kind switch {
            WindowBoundKind.UnboundedPreceding => "UNBOUNDED PRECEDING",
            WindowBoundKind.Preceding when b.Offset is not null => $"{EmitScalar(b.Offset)} PRECEDING",
            WindowBoundKind.CurrentRow => "CURRENT ROW",
            WindowBoundKind.Following when b.Offset is not null => $"{EmitScalar(b.Offset)} FOLLOWING",
            WindowBoundKind.UnboundedFollowing => "UNBOUNDED FOLLOWING",
            _ => throw new NotSupportedException($"Invalid window bound: {b.Kind}")
        };

        // ------------------------------------------------------------------
        // Function mappings — KQL → Proton / ClickHouse dialect
        // ------------------------------------------------------------------

        private string EmitFunction(FunctionCall fn)
        {
            var args = fn.Args.Select(EmitScalar).ToList();
            var name = fn.Name.ToLowerInvariant();

            return name switch {
                "tolower" => $"lower({args[0]})",
                "toupper" => $"upper({args[0]})",
                "strlen" => $"length({args[0]})",
                "strcat" => $"concat({string.Join(", ", args)})",
                "strcat_array" => $"arrayStringConcat({args[0]}, {args[1]})",
                "strcat_delim" => $"concat({string.Join(", ", args)})",
                "substring" => $"substring({args[0]}, ({args[1]}) + 1, {args[2]})",
                "replace_string" => $"replace({args[0]}, {args[1]}, {args[2]})",
                "replace_regex" => $"replaceRegexpAll({args[0]}, {args[1]}, {args[2]})",
                "split" => $"splitByString({args[1]}, {args[0]})",
                "indexof" => $"(position({args[0]}, {args[1]}) - 1)",
                "reverse" => $"reverse({args[0]})",
                "trim" => $"trimBoth({args[1]})",
                "trim_start" => $"trimLeft({args[1]})",
                "trim_end" => $"trimRight({args[1]})",
                "extract" => $"extract({args[2]}, {args[0]})",
                "countof" => $"((length({args[0]}) - length(replaceAll({args[0]}, {args[1]}, ''))) / nullif(length({args[1]}), 0))",

                "ago" => EmitAgo(fn.Args, args),
                "now" => "now()",
                "bin" when args.Count >= 2 => EmitBin(fn.Args, args),
                "startofday" => $"toStartOfDay({args[0]})",
                "startofmonth" => $"toStartOfMonth({args[0]})",
                "startofweek" => $"toStartOfWeek({args[0]})",
                "startofyear" => $"toStartOfYear({args[0]})",
                "endofday" => $"(toStartOfDay({args[0]}) + INTERVAL 1 DAY - INTERVAL 1 SECOND)",
                "endofmonth" => $"(toStartOfMonth({args[0]}) + INTERVAL 1 MONTH - INTERVAL 1 SECOND)",
                "endofweek" => $"(toStartOfWeek({args[0]}) + INTERVAL 7 DAY - INTERVAL 1 SECOND)",
                "endofyear" => $"(toStartOfYear({args[0]}) + INTERVAL 1 YEAR - INTERVAL 1 SECOND)",
                "datetime_diff" => EmitDatetimeDiff(fn.Args, args),
                "datetime_add" => EmitDatetimeAdd(fn.Args, args),
                "datetime_part" => EmitDatetimePart(fn.Args, args),
                "dayofweek" => $"toDayOfWeek({args[0]})",
                "dayofmonth" => $"toDayOfMonth({args[0]})",
                "dayofyear" => $"toDayOfYear({args[0]})",
                "monthofyear" or "getmonth" => $"toMonth({args[0]})",
                "getyear" => $"toYear({args[0]})",
                "hourofday" => $"toHour({args[0]})",
                "unixtime_seconds_todatetime" => $"fromUnixTimestamp({args[0]})",
                "unixtime_milliseconds_todatetime" => $"fromUnixTimestamp64Milli(to_int64({args[0]}))",
                "unixtime_microseconds_todatetime" => $"fromUnixTimestamp64Micro(to_int64({args[0]}))",
                "todatetime" => $"parseDateTimeBestEffort(toString({args[0]}))",

                "tostring" => $"toString({args[0]})",
                "tolong" => $"toInt64({args[0]})",
                "toint" => $"toInt32({args[0]})",
                "todouble" or "toreal" => $"toFloat64({args[0]})",
                "tobool" => $"toUInt8({args[0]})",
                "todecimal" => $"toDecimal64({args[0]}, 6)",
                "toguid" => $"toString({args[0]})",
                "parse_ipv4" => $"IPv4StringToNum({args[0]})",
                "base64_encode_tostring" => $"base64Encode({args[0]})",
                "base64_decode_tostring" => $"base64Decode({args[0]})",
                "url_encode" => $"encodeURLComponent({args[0]})",
                "url_decode" => $"decodeURLComponent({args[0]})",
                "hash_sha256" => $"hex(SHA256({args[0]}))",
                "hash_md5" => $"hex(MD5({args[0]}))",
                "translate" => $"translate({args[2]}, {args[0]}, {args[1]})",

                "iff" or "iif" => $"if({args[0]}, {args[1]}, {args[2]})",
                "coalesce" => $"coalesce({string.Join(", ", args)})",
                "max_of" => $"greatest({string.Join(", ", args)})",
                "min_of" => $"least({string.Join(", ", args)})",

                "isnull" => $"({args[0]} IS NULL)",
                "isnotnull" => $"({args[0]} IS NOT NULL)",
                "isempty" => $"({args[0]} IS NULL OR {args[0]} = '')",
                "isnotempty" => $"({args[0]} IS NOT NULL AND {args[0]} != '')",
                "isnan" => $"isNaN({args[0]})",
                "isinf" => $"isInfinite({args[0]})",

                "parse_json" => args[0],
                "bag_keys" => $"JSONExtractKeys({args[0]})",
                "bag_has_key" => $"(JSONHas({args[0]}, {args[1]}))",
                "array_length" => $"length({args[0]})",
                "array_concat" => $"arrayConcat({args[0]}, {args[1]})",

                "abs" => $"abs({args[0]})",
                "ceiling" => $"ceil({args[0]})",
                "floor" => $"floor({args[0]})",
                "round" => $"round({args[0]}, {args[1]})",
                "log" => $"log({args[0]})",
                "log2" => $"log2({args[0]})",
                "log10" => $"log10({args[0]})",
                "pow" => $"pow({args[0]}, {args[1]})",
                "sqrt" => $"sqrt({args[0]})",
                "exp" => $"exp({args[0]})",
                "exp2" => $"exp2({args[0]})",
                "exp10" => $"exp10({args[0]})",
                "sign" => $"sign({args[0]})",
                "pi" => "pi()",
                "rand" => "rand()",
                "cos" => $"cos({args[0]})",
                "sin" => $"sin({args[0]})",
                "tan" => $"tan({args[0]})",
                "acos" => $"acos({args[0]})",
                "asin" => $"asin({args[0]})",
                "atan" => $"atan({args[0]})",
                "atan2" => $"atan2({args[0]}, {args[1]})",

                "not" when args.Count == 1 => $"NOT ({args[0]})",

                "prev" => $"lagInFrame({args[0]})",
                "next" => $"leadInFrame({args[0]})",

                "count" => args.Count == 0 ? "count()" : $"count({args[0]})",
                "countif" => $"countIf({args[0]})",
                "sum" => $"sum({args[0]})",
                "sumif" => $"sumIf({args[0]}, {args[1]})",
                "avg" => $"avg({args[0]})",
                "avgif" => $"avgIf({args[0]}, {args[1]})",
                "min" => $"min({args[0]})",
                "max" => $"max({args[0]})",
                "dcount" => $"uniq({args[0]})",
                "dcountif" => $"uniqIf({args[0]}, {args[1]})",
                "arg_min" => $"argMin({string.Join(", ", args)})",
                "arg_max" => $"argMax({string.Join(", ", args)})",
                "make_set" when args.Count == 1 => $"groupUniqArray({args[0]})",
                "make_set" => $"arraySlice(groupUniqArray({args[0]}), 1, {args[1]})",
                "make_list" when args.Count == 1 => $"groupArray({args[0]})",
                "make_list" => $"arraySlice(groupArray({args[0]}), 1, {args[1]})",
                "any" => $"any({args[0]})",
                "stdev" => $"stddevSamp({args[0]})",
                "stdevif" => $"stddevSampIf({args[0]}, {args[1]})",
                "variance" => $"varSamp({args[0]})",
                "varianceif" => $"varSampIf({args[0]}, {args[1]})",
                "percentile" when _inAggregateProjection => $"quantile({args[1]} / 100.0)({args[0]})",
                "percentile" => throw new NotSupportedException("percentile() only supported inside summarize."),
                "binary_all_and" => $"groupBitAnd({args[0]})",
                "binary_all_or" => $"groupBitOr({args[0]})",
                "binary_all_xor" => $"groupBitXor({args[0]})",
                "row_number" => "row_number()",

                _ => throw new NotSupportedException(
                    $"KQL function '{name}' has no Proton mapping. Add it to ProtonSqlQueryEmitter.")
            };
        }

        private static string EmitAgo(IReadOnlyList<ScalarExpr> rawArgs, List<string> args) =>
            $"(now() - {args[0]})";

        private static string EmitBin(IReadOnlyList<ScalarExpr> rawArgs, List<string> args)
        {
            if (rawArgs[1] is LiteralScalar { Kind: LiteralKind.Timespan })
            {
                return $"toStartOfInterval({args[0]}, {args[1]})";
            }

            return $"(floor(({args[0]}) / ({args[1]})) * ({args[1]}))";
        }

        private static string EmitDatetimeDiff(IReadOnlyList<ScalarExpr> rawArgs, List<string> args) =>
            $"dateDiff({args[0]}, {args[2]}, {args[1]})";

        private static string EmitDatetimeAdd(IReadOnlyList<ScalarExpr> rawArgs, List<string> args)
        {
            if (rawArgs[0] is LiteralScalar { Value: string partName })
            {
                var unit = partName.ToLowerInvariant() switch {
                    "year" => "YEAR",
                    "month" => "MONTH",
                    "week" => "WEEK",
                    "day" => "DAY",
                    "hour" => "HOUR",
                    "minute" => "MINUTE",
                    "second" => "SECOND",
                    "millisecond" => "MILLISECOND",
                    _ => partName.ToUpperInvariant()
                };
                return $"({args[2]} + INTERVAL {args[1]} {unit})";
            }
            return $"date_add({args[0]}, {args[1]}, {args[2]})";
        }

        private static string EmitDatetimePart(IReadOnlyList<ScalarExpr> rawArgs, List<string> args)
        {
            if (rawArgs[0] is LiteralScalar { Value: string partName })
            {
                return partName.ToLowerInvariant() switch {
                    "year" => $"toYear({args[1]})",
                    "month" => $"toMonth({args[1]})",
                    "day" => $"toDayOfMonth({args[1]})",
                    "hour" => $"toHour({args[1]})",
                    "minute" => $"toMinute({args[1]})",
                    "second" => $"toSecond({args[1]})",
                    _ => $"toYear({args[1]})"
                };
            }
            return $"toYear({args[1]})";
        }

        private static string EmitInterval(object value)
        {
            var ts = value.ToString()!;

            if (ts.Contains(':') && TimeSpan.TryParse(ts, out var parsed))
            {
                var parts = new List<string>();
                if (parsed.Days != 0)
                {
                    parts.Add($"INTERVAL {parsed.Days} DAY");
                }

                if (parsed.Hours != 0)
                {
                    parts.Add($"INTERVAL {parsed.Hours} HOUR");
                }

                if (parsed.Minutes != 0)
                {
                    parts.Add($"INTERVAL {parsed.Minutes} MINUTE");
                }

                if (parsed.Seconds != 0)
                {
                    parts.Add($"INTERVAL {parsed.Seconds} SECOND");
                }

                if (parsed.Milliseconds != 0)
                {
                    parts.Add($"INTERVAL {parsed.Milliseconds} MILLISECOND");
                }

                return parts.Count == 0 ? "INTERVAL 0 SECOND" : string.Join(" + ", parts);
            }

            if (ts.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
            {
                return $"INTERVAL {ts[..^2]} MILLISECOND";
            }

            if (ts.EndsWith("us", StringComparison.OrdinalIgnoreCase))
            {
                return $"INTERVAL {ts[..^2]} MICROSECOND";
            }

            if (ts.EndsWith("d", StringComparison.OrdinalIgnoreCase))
            {
                return $"INTERVAL {ts[..^1]} DAY";
            }

            if (ts.EndsWith("h", StringComparison.OrdinalIgnoreCase))
            {
                return $"INTERVAL {ts[..^1]} HOUR";
            }

            if (ts.EndsWith("m", StringComparison.OrdinalIgnoreCase))
            {
                return $"INTERVAL {ts[..^1]} MINUTE";
            }

            if (ts.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                return $"INTERVAL {ts[..^1]} SECOND";
            }

            return $"INTERVAL {ts} SECOND";
        }

        private static string QuoteIdent(string name) =>
            name.All(c => char.IsLetterOrDigit(c) || c == '_') ? name : $"`{name.Replace("`", "``")}`";

        private static string EscapeString(string s) => s.Replace("'", "''");
    }
}