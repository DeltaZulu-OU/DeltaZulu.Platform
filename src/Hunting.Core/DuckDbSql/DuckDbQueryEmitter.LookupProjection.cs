namespace Hunting.Core.DuckDbSql;

using System.Text;
using QueryModel;

public sealed partial class DuckDbQueryEmitter
{
    private bool TryRenderProjectedLookupSortTake(RelNode node, out string sql)
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

        var leftIndex = GetStageIndex(leftSource);
        var rightIndex = GetStageIndex(rightSource);
        if (leftIndex < 0 || rightIndex < 0)
        {
            return false;
        }

        var leftSql = _ctes[leftIndex].Sql;
        var rightSql = _ctes[rightIndex].Sql;
        if (StageRefRegex().IsMatch(leftSql) || StageRefRegex().IsMatch(rightSql))
        {
            return false;
        }

        _joinLeftAlias = "left_agg";
        _joinRightAlias = "right_agg";

        string onPredicate;
        try
        {
            onPredicate = TrimSingleOuterParentheses(EmitScalar(join.OnPredicate));
        }
        finally
        {
            _joinLeftAlias = null;
            _joinRightAlias = null;
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
        sb.AppendLine(IndentSql(leftSql));
        sb.AppendLine("),");
        sb.AppendLine("right_agg AS (");
        sb.AppendLine(IndentSql(rightSql));
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
            return $"{resolved} AS {EscapeIdent(projection.Alias)}";
        }

        return $"{EmitScalar(projection.Expression)} AS {EscapeIdent(projection.Alias)}";
    }

    private string EmitLookupFinalSort(
        SortExpr sort,
        IReadOnlySet<string> leftColumns,
        IReadOnlySet<string> rightColumns,
        IReadOnlySet<string> joinKeys)
    {
        var expression = sort.Expression is ColumnRef column
            ? ResolveLookupFinalColumn(column.Name, leftColumns, rightColumns, joinKeys)
            : EmitScalar(sort.Expression);

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
            if (usage > 1)
            {
                continue;
            }

            substitutions[cte.Name] = match.Groups[1].Value;
            RemoveStageAt(i);
        }

        if (substitutions.Count == 0)
        {
            return;
        }

        for (var i = 0; i < _ctes.Count; i++)
        {
            var sql = StageRefRegex().Replace(
                _ctes[i].Sql,
                m => substitutions.TryGetValue(m.Value, out var replacement) ? replacement : m.Value);

            _ctes[i] = (_ctes[i].Name, sql);
        }

        InvalidateStageCaches();
    }

    private static string IndentSql(string sql)
    {
        var normalized = sql.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        return string.Join(
            Environment.NewLine,
            lines.Select(line => "    " + line));
    }
}
