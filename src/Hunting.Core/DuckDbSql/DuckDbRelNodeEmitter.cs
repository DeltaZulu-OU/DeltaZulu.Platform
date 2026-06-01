namespace Hunting.Core.DuckDbSql;

using System.Text;
using QueryModel;

internal sealed partial class DuckDbRelNodeEmitter
{
    internal string Emit(RelNode node)
    {
        if (_joinEmitter.TryRenderProjectedLookupSortTake(node, out var projectedLookupSql))
        {
            return projectedLookupSql;
        }

        var shapeRewriter = new DuckDbSqlShapeRewriter(_context.Stages);
        var (finalSource, columns) = EmitNode(node);
        var hasUserLimit = HasLimit(node);
        var terminalTopK = shapeRewriter.ShapeSql(ref finalSource);
        var terminalOrder = shapeRewriter.TryExtractTerminalOrder(finalSource);
        if (terminalOrder is not null)
        {
            finalSource = terminalOrder.Source;
        }
        DuckDbSqlShapeRewriter.TerminalLimit? terminalLimit = null;
        shapeRewriter.TryInlineSingleUseAggregateStageIntoTerminalTopK(ref terminalTopK, ref columns);
        shapeRewriter.TryInlineSingleUseAggregateStageIntoTerminalOrder(ref terminalOrder, ref columns);
        shapeRewriter.TryInlineSingleUseAggregateFilterStageIntoTerminalOrder(ref terminalOrder, ref columns);
        if (terminalOrder is not null)
        {
            // Aggregate inlining into terminal ORDER may rewrite its source.
            // Keep finalSource in sync before subsequent inlining passes run.
            finalSource = terminalOrder.Source;
        }
        shapeRewriter.TryInlineSingleUseFilterStageIntoProjection(ref finalSource, ref columns);
        shapeRewriter.TryInlineFinalStage(ref finalSource, ref columns);
        // Final-stage inlining can materialize the projection list from a stage source.
        // Re-run filter inlining once so where|project|take shapes can collapse fully.
        shapeRewriter.TryInlineSingleUseFilterStageIntoProjection(ref finalSource, ref columns);
        // A filter-inline pass can produce "<stage> WHERE <pred>" where <stage> is now
        // a trivial pass-through CTE ("SELECT * FROM golden.X"). Collapse that final
        // wrapper so optimized where|where|project shapes do not retain a leading WITH.
        shapeRewriter.TryInlinePassThroughBaseStage(ref finalSource);
        if (terminalOrder is not null)
        {
            // Keep terminal ORDER source aligned with later projection/filter inlining.
            // Without this, we can remove an intermediate stage from _context.Stages.Ctes and still
            // reference it in final FROM (dangling __kql_stage_N).
            terminalOrder = terminalOrder with { Source = finalSource };
        }
        if (terminalTopK is null && terminalOrder is null)
        {
            terminalLimit = shapeRewriter.TryExtractTerminalLimit(finalSource);
            if (terminalLimit is not null)
            {
                finalSource = terminalLimit.Source;
                // LIMIT extraction can expose a terminal projection stage.
                // Inline it (and then its single-use filter input) to fully
                // collapse where|project|take into one SELECT block.
                shapeRewriter.TryInlineFinalStage(ref finalSource, ref columns);
                shapeRewriter.TryInlineSingleUseFilterStageIntoProjection(ref finalSource, ref columns);
            }
        }
        if (terminalLimit is not null)
        {
            // Keep terminal LIMIT source aligned with later projection/filter inlining.
            // Without this, we can remove an intermediate stage from _context.Stages.Ctes and still
            // reference it in final FROM (dangling __kql_stage_N).
            terminalLimit = terminalLimit with { Source = finalSource };
        }
        _joinEmitter.TryCollapseProjectedLookupJoin(ref finalSource, ref columns, ref terminalTopK, ref terminalOrder, ref terminalLimit);
        if (shapeRewriter.TryRenderDerivedComputedScope(columns, terminalTopK, terminalOrder, terminalLimit, out var derivedSql))
        {
            return derivedSql;
        }

        var sb = new StringBuilder();

        if (_context.Stages.Ctes.Count > 0)
        {
            sb.Append("WITH ");
            for (var i = 0; i < _context.Stages.Ctes.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(_context.Stages.Ctes[i].Name).Append(" AS (").Append(_context.Stages.Ctes[i].Sql).Append(')');
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
            if (!hasUserLimit && _context.Options.ApplyDefaultLimit)
            {
                sb.Append(" LIMIT ").Append(_context.Options.DefaultLimit);
            }
        }
        else if (terminalLimit is not null)
        {
            sb.Append(" LIMIT ").Append(terminalLimit.Limit);
        }
        else if (!hasUserLimit && _context.Options.ApplyDefaultLimit)
        {
            sb.Append(" LIMIT ").Append(_context.Options.DefaultLimit);
        }

        var sqlText = sb.ToString();
        return sqlText;
    }

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

}
