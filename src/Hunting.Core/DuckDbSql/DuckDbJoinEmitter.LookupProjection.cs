namespace Hunting.Core.DuckDbSql;

using System.Text;
using System.Text.RegularExpressions;
using QueryModel;

internal sealed partial class DuckDbJoinEmitter
{
    internal bool TryRenderProjectedLookupSortTake(RelNode node, out string sql)
    {
        sql = string.Empty;

        if (node is not LimitNode
            {
                Input: SortNode
                {
                    Input: ProjectNode
                    {
                        Input: JoinNode
                        {
                            Flavor: JoinFlavor.Lookup,
                            Kind: JoinKind.LeftOuter
                        } join
                    } project,
                    Sorts: var sorts
                },
                Count: var limit
            })
        {
            return false;
        }

        var leftColumns = TryGetOutputColumns(join.Left);
        var rightColumns = TryGetOutputColumns(join.Right);
        if (leftColumns is null || rightColumns is null)
        {
            return false;
        }

        var leftColumnSet = leftColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rightColumnSet = rightColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var joinKeys = CollectLookupJoinKeys(join.OnPredicate);

        var (leftSource, _) = EmitNode(join.Left);
        var (rightSource, _) = EmitNode(join.Right);

        InlineTrivialBaseStagesForLookupRendering();

        var leftIndex = _context.Stages.GetStageIndex(leftSource);
        var rightIndex = _context.Stages.GetStageIndex(rightSource);
        if (leftIndex < 0 || rightIndex < 0)
        {
            return false;
        }

        var leftSql = _context.Stages.Ctes[leftIndex].Sql;
        var rightSql = _context.Stages.Ctes[rightIndex].Sql;
        if (DuckDbQueryEmitter.StageRefRegex().IsMatch(leftSql) || DuckDbQueryEmitter.StageRefRegex().IsMatch(rightSql))
        {
            return false;
        }

        _context.JoinLeftAlias = "left_agg";
        _context.JoinRightAlias = "right_agg";

        string onPredicate;
        try
        {
            onPredicate = TrimSingleOuterParentheses(_scalarEmitter.EmitScalar(join.OnPredicate));
        }
        finally
        {
            _context.JoinLeftAlias = null;
            _context.JoinRightAlias = null;
        }

        var projectionSql = string.Join(
            ", ",
            project.Projections.Select(projection =>
                EmitLookupFinalProjection(projection, leftColumnSet, rightColumnSet, joinKeys)));

        var orderBySql = string.Join(
            ", ",
            sorts.Select(sort =>
                EmitLookupFinalSort(sort, leftColumnSet, rightColumnSet, joinKeys)));

        var sb = new StringBuilder();
        sb.AppendLine("WITH");
        sb.AppendLine("left_agg AS (");
        sb.AppendLine(DuckDbSqlText.IndentSql(leftSql));
        sb.AppendLine("),");
        sb.AppendLine("right_agg AS (");
        sb.AppendLine(DuckDbSqlText.IndentSql(rightSql));
        sb.AppendLine(")");
        sb.Append("SELECT ");
        sb.Append(projectionSql);
        sb.Append(" FROM left_agg LEFT JOIN right_agg ON ");
        sb.Append(onPredicate);
        sb.Append(" ORDER BY ");
        sb.Append(orderBySql);
        sb.Append(" LIMIT ");
        sb.Append(limit);

        sql = sb.ToString();
        return true;
    }

    private string EmitLookupFinalProjection(
    ProjectionExpr projection,
    IReadOnlySet<string> leftColumns,
    IReadOnlySet<string> rightColumns,
    IReadOnlySet<string> joinKeys)
    {
        if (projection.Expression is ColumnRef column)
        {
            var resolved = ResolveLookupFinalColumn(column.Name, leftColumns, rightColumns, joinKeys);
            return $"{resolved} AS {DuckDbSqlText.EscapeIdent(projection.Alias)}";
        }

        return $"{_scalarEmitter.EmitScalar(projection.Expression)} AS {DuckDbSqlText.EscapeIdent(projection.Alias)}";
    }

    private string EmitLookupFinalSort(
        SortExpr sort,
        IReadOnlySet<string> leftColumns,
        IReadOnlySet<string> rightColumns,
        IReadOnlySet<string> joinKeys)
    {
        var expression = sort.Expression is ColumnRef column
            ? ResolveLookupFinalColumn(column.Name, leftColumns, rightColumns, joinKeys)
            : _scalarEmitter.EmitScalar(sort.Expression);

        var direction = sort.Direction == SortDirection.Desc ? " DESC" : " ASC";
        var nulls = sort.Nulls switch
        {
            NullOrder.First => " NULLS FIRST",
            NullOrder.Last => " NULLS LAST",
            _ => sort.Direction == SortDirection.Asc ? " NULLS FIRST" : " NULLS LAST"
        };

        return $"{expression}{direction}{nulls}";
    }

    private static string ResolveLookupFinalColumn(
        string columnName,
        IReadOnlySet<string> leftColumns,
        IReadOnlySet<string> rightColumns,
        IReadOnlySet<string> joinKeys)
    {
        if (joinKeys.Contains(columnName) && leftColumns.Contains(columnName))
        {
            return $"left_agg.{columnName}";
        }

        if (leftColumns.Contains(columnName))
        {
            return $"left_agg.{columnName}";
        }

        if (rightColumns.Contains(columnName))
        {
            return $"right_agg.{columnName}";
        }

        throw new InvalidOperationException(
            $"Column '{columnName}' is not available in the lookup output scope.");
    }

    private static IReadOnlySet<string> CollectLookupJoinKeys(ScalarExpr predicate)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Walk(ScalarExpr expression)
        {
            switch (expression)
            {
                case BinaryScalar { Op: ScalarBinaryOp.And, Left: var left, Right: var right }:
                    Walk(left);
                    Walk(right);
                    break;

                case BinaryScalar
                {
                    Op: ScalarBinaryOp.Eq,
                    Left: ColumnRef { Qualifier: JoinSide.Left } left,
                    Right: ColumnRef { Qualifier: JoinSide.Right } right
                } when left.Name.Equals(right.Name, StringComparison.OrdinalIgnoreCase):
                    keys.Add(left.Name);
                    break;

                case BinaryScalar
                {
                    Op: ScalarBinaryOp.Eq,
                    Left: ColumnRef { Qualifier: JoinSide.Right } right,
                    Right: ColumnRef { Qualifier: JoinSide.Left } left
                } when left.Name.Equals(right.Name, StringComparison.OrdinalIgnoreCase):
                    keys.Add(left.Name);
                    break;
            }
        }

        Walk(predicate);
        return keys;
    }

    private void InlineTrivialBaseStagesForLookupRendering()
    {
        var substitutions = new Dictionary<string, string>(StringComparer.Ordinal);
        var refCounts = _context.Stages.BuildStageRefCounts();

        for (var i = _context.Stages.Ctes.Count - 1; i >= 0; i--)
        {
            var cte = _context.Stages.Ctes[i];
            var match = DuckDbSqlShapeRewriter.TrivialSourceStageRegex().Match(cte.Sql);
            if (!match.Success)
            {
                continue;
            }

            var usage = refCounts.TryGetValue(cte.Name, out var count) ? count : 0;
            if (usage > 1)
            {
                continue;
            }

            substitutions[cte.Name] = match.Groups[1].Value;
            _context.Stages.RemoveStageAt(i);
        }

        if (substitutions.Count == 0)
        {
            return;
        }

        for (var i = 0; i < _context.Stages.Ctes.Count; i++)
        {
            var sql = DuckDbQueryEmitter.StageRefRegex().Replace(
                _context.Stages.Ctes[i].Sql,
                m => substitutions.TryGetValue(m.Value, out var replacement) ? replacement : m.Value);

            _context.Stages.ReplaceStageSql(i, sql);
        }

        _context.Stages.InvalidateCaches();
    }

    [GeneratedRegex(
        @"^SELECT (?<projection>.+) FROM (?<joinStage>__kql_stage_\d+)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex JoinProjectionStageRegex();

    [GeneratedRegex(
        @"^SELECT \* FROM (?<left>[A-Za-z0-9_.]+) AS __join_left LEFT JOIN (?<right>[A-Za-z0-9_.]+) AS __join_right ON \((?<pred>.+)\)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex LeftLookupJoinStageRegex();

    [GeneratedRegex(
        @"^SELECT __join_left\.\*(?:, (?<payload>.+))? FROM (?<left>[A-Za-z0-9_.]+) AS __join_left LEFT JOIN (?<right>[A-Za-z0-9_.]+) AS __join_right ON \((?<pred>.+)\)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex LeftLookupProjectedJoinStageRegex();

    internal void TryCollapseProjectedLookupJoin(
        ref string finalSource,
        ref string? columns,
        ref DuckDbSqlShapeRewriter.TerminalTopK? terminalTopK,
        ref DuckDbSqlShapeRewriter.TerminalOrder? terminalOrder,
        ref DuckDbSqlShapeRewriter.TerminalLimit? terminalLimit)
    {
        var terminalSource = terminalTopK?.Source ?? terminalOrder?.Source ?? terminalLimit?.Source ?? finalSource;
        var projectionIndex = _context.Stages.GetStageIndex(terminalSource);
        if (projectionIndex < 0)
        {
            return;
        }

        var projectionMatch = JoinProjectionStageRegex().Match(_context.Stages.Ctes[projectionIndex].Sql);
        if (!projectionMatch.Success)
        {
            return;
        }

        var joinStageName = projectionMatch.Groups["joinStage"].Value;
        var joinIndex = _context.Stages.GetStageIndex(joinStageName);
        if (joinIndex < 0)
        {
            return;
        }

        static string RenameJoinAliases(string text) =>
            text.Replace("__join_left.", "left_agg.", StringComparison.Ordinal)
                .Replace("__join_right.", "right_agg.", StringComparison.Ordinal);

        var plainMatch = LeftLookupJoinStageRegex().Match(_context.Stages.Ctes[joinIndex].Sql);
        if (plainMatch.Success)
        {
            var projected = RenameJoinAliases(projectionMatch.Groups["projection"].Value);
            var plainJoinSource =
                $"{plainMatch.Groups["left"].Value} AS left_agg LEFT JOIN {plainMatch.Groups["right"].Value} AS right_agg ON {RenameJoinAliases(plainMatch.Groups["pred"].Value)}";

            // Remove the higher index first so the lower index stays valid.
            var plainHigherIndex = Math.Max(projectionIndex, joinIndex);
            var plainLowerIndex = Math.Min(projectionIndex, joinIndex);
            _context.Stages.RemoveStageAt(plainHigherIndex);
            _context.Stages.RemoveStageAt(plainLowerIndex);

            finalSource = plainJoinSource;
            columns = projected;

            if (terminalTopK is not null)
            {
                terminalTopK = terminalTopK with
                {
                    Source = plainJoinSource,
                    OrderBy = RenameJoinAliases(terminalTopK.OrderBy)
                };
            }

            if (terminalOrder is not null)
            {
                terminalOrder = terminalOrder with
                {
                    Source = plainJoinSource,
                    OrderBy = RenameJoinAliases(terminalOrder.OrderBy)
                };
            }

            if (terminalLimit is not null)
            {
                terminalLimit = terminalLimit with { Source = plainJoinSource };
            }

            return;
        }
        // A `lookup` join emits `SELECT __join_left.*, __join_right.<cols> FROM ...`
        // rather than `SELECT *`, so the plain rule above never matches it. Fold the
        // trailing projection into a fully-qualified SELECT directly over the join
        // and drop both intermediate CTEs. Qualification is mandatory here: the join
        // key column lives on both inputs, so a bare reference would be ambiguous.
        var lookupMatch = LeftLookupProjectedJoinStageRegex().Match(_context.Stages.Ctes[joinIndex].Sql);
        if (!lookupMatch.Success)
        {
            return;
        }

        var rightOwned = ParseRightPayloadColumns(lookupMatch.Groups["payload"].Value);
        if (rightOwned is null
            || !TryQualifyLookupProjection(projectionMatch.Groups["projection"].Value, rightOwned, out var qualifiedProjection))
        {
            return;
        }

        var inlinedJoin =
            $"{lookupMatch.Groups["left"].Value} AS left_agg LEFT JOIN {lookupMatch.Groups["right"].Value} AS right_agg ON {RenameJoinAliases(lookupMatch.Groups["pred"].Value)}";

        // Remove the higher index first so the lower index stays valid.
        var higher = Math.Max(projectionIndex, joinIndex);
        var lower = Math.Min(projectionIndex, joinIndex);
        _context.Stages.RemoveStagesAtWithoutTracking(higher, lower);

        finalSource = inlinedJoin;
        columns = qualifiedProjection;
        if (terminalTopK is not null)
        {
            terminalTopK = terminalTopK with { Source = inlinedJoin, OrderBy = RenameJoinAliases(terminalTopK.OrderBy) };
        }

        if (terminalOrder is not null)
        {
            terminalOrder = terminalOrder with { Source = inlinedJoin, OrderBy = RenameJoinAliases(terminalOrder.OrderBy) };
        }

        if (terminalLimit is not null)
        {
            terminalLimit = terminalLimit with { Source = inlinedJoin };
        }
    }

    private string BindProjectedLookupJoinColumn(
        string projectionItem,
        IReadOnlyDictionary<string, string> leftColumns,
        IReadOnlyDictionary<string, string> rightColumns,
        IReadOnlySet<string> joinKeys)
    {
        var renamed = projectionItem
            .Replace("__join_left.", "left_agg.", StringComparison.Ordinal)
            .Replace("__join_right.", "right_agg.", StringComparison.Ordinal);

        if (renamed.Contains("left_agg.", StringComparison.Ordinal) ||
            renamed.Contains("right_agg.", StringComparison.Ordinal))
        {
            return renamed;
        }

        var (sourceColumn, alias) = ExtractLookupProjectionColumn(projectionItem);
        if (string.IsNullOrWhiteSpace(sourceColumn))
        {
            return renamed;
        }

        if (joinKeys.Contains(sourceColumn) && leftColumns.TryGetValue(sourceColumn, out var leftJoinKey))
        {
            return $"{leftJoinKey} AS {alias}";
        }

        if (leftColumns.TryGetValue(sourceColumn, out var leftColumn))
        {
            return $"{leftColumn} AS {alias}";
        }

        if (rightColumns.TryGetValue(sourceColumn, out var rightColumn))
        {
            return $"{rightColumn} AS {alias}";
        }

        throw new InvalidOperationException(
            $"Unknown lookup projection column '{sourceColumn}' while collapsing projected lookup join.");
    }

    private static string TrimSingleOuterParentheses(string value)
    {
        var text = value.Trim();

        if (text.Length < 2 || text[0] != '(' || text[^1] != ')')
        {
            return text;
        }

        var depth = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '(')
            {
                depth++;
            }
            else if (text[i] == ')')
            {
                depth--;

                if (depth == 0 && i < text.Length - 1)
                {
                    return text;
                }
            }
        }

        return depth == 0 ? text[1..^1].Trim() : text;
    }

    private static (string SourceColumn, string Alias) ExtractLookupProjectionColumn(string projectionItem)
    {
        var trimmed = projectionItem.Trim();
        var asMatch = AiasMatchPattern().Match(trimmed);

        if (asMatch.Success)
        {
            return (
                TrimLookupColumnQualifier(asMatch.Groups["expr"].Value),
                asMatch.Groups["alias"].Value);
        }

        if (LookupColumnPattern().IsMatch(trimmed))
        {
            var column = TrimLookupColumnQualifier(trimmed);
            return (column, column);
        }

        return (string.Empty, ExtractLookupProjectionAlias(trimmed));
    }

    private static string ExtractLookupProjectionAlias(string projectionItem)
    {
        var trimmed = projectionItem.Trim();
        var asMatch = LookupProjectionAliasPattern().Match(trimmed);

        if (asMatch.Success)
        {
            return asMatch.Groups["alias"].Value;
        }

        return TrimLookupColumnQualifier(trimmed);
    }

    private static string TrimLookupColumnQualifier(string column)
    {
        var trimmed = column.Trim();
        var dot = trimmed.LastIndexOf('.');
        return dot >= 0 ? trimmed[(dot + 1)..] : trimmed;
    }

    /// <summary>
    /// Parse the right-side payload of a lookup join's select list
    /// (<c>__join_right.Col1, __join_right.Col2</c>) into the set of column names
    /// owned by the right input. Returns an empty set when the right side carries
    /// only the join key, or null when any entry is not a simple
    /// <c>__join_right.&lt;identifier&gt;</c> reference (in which case the caller
    /// must not attempt the collapse).
    /// </summary>
    private static HashSet<string>? ParseRightPayloadColumns(string payload)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(payload))
        {
            return set;
        }

        foreach (var raw in payload.Split(", ", StringSplitOptions.None))
        {
            const string prefix = "__join_right.";
            var item = raw.Trim();
            if (!item.StartsWith(prefix, StringComparison.Ordinal))
            {
                return null;
            }

            var col = item[prefix.Length..];
            if (!IsSimpleIdentifier(col))
            {
                return null;
            }

            set.Add(col);
        }

        return set;
    }

    /// <summary>
    /// Rewrite a lookup-join output projection so each column is qualified by the
    /// side that owns it (<c>left_agg</c> or <c>right_agg</c>) and aliased to its
    /// output name. Returns false unless every projection item is a simple column
    /// reference (optionally <c>col AS alias</c>); a computed projection is left
    /// for the unoptimized path rather than risk mis-qualifying it.
    /// </summary>
    private static bool TryQualifyLookupProjection(
        string projection,
        HashSet<string> rightOwned,
        out string qualified)
    {
        qualified = string.Empty;
        var rendered = new List<string>();
        foreach (var raw in projection.Split(", ", StringSplitOptions.None))
        {
            var item = raw.Trim();
            string col;
            string alias;
            var asIndex = item.IndexOf(" AS ", StringComparison.OrdinalIgnoreCase);
            if (asIndex >= 0)
            {
                col = item[..asIndex].Trim();
                alias = item[(asIndex + 4)..].Trim();
            }
            else
            {
                col = item;
                alias = item;
            }

            if (!IsSimpleIdentifier(col) || !IsSimpleIdentifier(alias))
            {
                return false;
            }

            var side = rightOwned.Contains(col) ? "right_agg" : "left_agg";
            rendered.Add($"{side}.{col} AS {alias}");
        }

        if (rendered.Count == 0)
        {
            return false;
        }

        qualified = string.Join(", ", rendered);
        return true;
    }

    private static bool IsSimpleIdentifier(string s) =>
        s.Length > 0 && !char.IsDigit(s[0]) && s.All(c => char.IsLetterOrDigit(c) || c == '_');

    #region Join


    private static bool TryBuildLookupJoinProjection(
        RelNode right,
        ScalarExpr predicate,
        out IReadOnlyList<string> rightPayloadColumns)
    {
        var rightCols = TryGetOutputColumns(right);
        if (rightCols is null)
        {
            rightPayloadColumns = [];
            return false;
        }

        var rightKeys = CollectRightJoinKeys(predicate);
        var payload = new List<string>(rightCols.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in rightCols)
        {
            if (rightKeys.Contains(col) || !seen.Add(col))
            {
                continue;
            }

            payload.Add(col);
        }

        rightPayloadColumns = payload;
        return true;
    }

    private static IReadOnlyList<string>? TryGetOutputColumns(RelNode node)
    {
        switch (node)
        {
            case ProjectNode p:
                {
                    var cols = new List<string>(p.Projections.Count);
                    foreach (var projection in p.Projections)
                    {
                        cols.Add(projection.Alias);
                    }

                    return cols;
                }
            case AggregateNode a:
                {
                    var cols = new List<string>(a.GroupBy.Count + a.Aggregates.Count);
                    foreach (var group in a.GroupBy)
                    {
                        if (group is ColumnRef c)
                        {
                            cols.Add(c.Name);
                        }
                    }

                    foreach (var aggregate in a.Aggregates)
                    {
                        cols.Add(aggregate.Alias);
                    }

                    return cols;
                }
            case ExtendNode e:
                {
                    var input = TryGetOutputColumns(e.Input) ?? [];
                    var cols = new List<string>(input.Count + e.Extensions.Count);
                    cols.AddRange(input);
                    foreach (var extension in e.Extensions)
                    {
                        cols.Add(extension.Alias);
                    }

                    return cols;
                }
            case DistinctNode d:
                {
                    var cols = new List<string>(d.Projections.Count);
                    foreach (var projection in d.Projections)
                    {
                        cols.Add(projection.Alias);
                    }

                    return cols;
                }
            case FilterNode f:
                return TryGetOutputColumns(f.Input);
            case SortNode s:
                return TryGetOutputColumns(s.Input);
            case LimitNode l:
                return TryGetOutputColumns(l.Input);
            case SampleNode s:
                return TryGetOutputColumns(s.Input);
            default:
                return null;
        }
    }

    private static HashSet<string> CollectRightJoinKeys(ScalarExpr expr)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Walk(ScalarExpr e)
        {
            if (e is BinaryScalar b && b.Op == ScalarBinaryOp.And)
            {
                Walk(b.Left);
                Walk(b.Right);
                return;
            }

            if (e is BinaryScalar { Op: ScalarBinaryOp.Eq, Right: ColumnRef { Qualifier: JoinSide.Right } rc })
            {
                keys.Add(rc.Name);
            }
        }

        Walk(expr);
        return keys;
    }

    #endregion Join


    [GeneratedRegex(@"^(?<expr>[A-Za-z_][A-Za-z0-9_\.]*)\s+AS\s+(?<alias>[A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.IgnoreCase, "en-150")]
    private static partial Regex AiasMatchPattern();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_\.]*$")]
    private static partial Regex LookupColumnPattern();

    [GeneratedRegex(@"\s+AS\s+(?<alias>[A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.IgnoreCase, "en-150")]
    private static partial Regex LookupProjectionAliasPattern();
    private (string Source, string? Columns) EmitNode(RelNode node) =>
        (_emitNode ?? throw new InvalidOperationException("Relational emitter callbacks are not bound."))(node);

}
