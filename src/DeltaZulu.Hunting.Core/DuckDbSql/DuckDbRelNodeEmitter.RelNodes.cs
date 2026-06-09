namespace DeltaZulu.Hunting.Core.DuckDbSql;

using QueryModel;

internal sealed partial class DuckDbRelNodeEmitter
{
    private readonly DuckDbEmitterContext _context;
    private readonly DuckDbScalarEmitter _scalarEmitter;
    private readonly DuckDbJoinEmitter _joinEmitter;

    internal DuckDbRelNodeEmitter(
        DuckDbEmitterContext context,
        DuckDbScalarEmitter scalarEmitter,
        DuckDbJoinEmitter joinEmitter)
    {
        _context = context;
        _scalarEmitter = scalarEmitter;
        _joinEmitter = joinEmitter;
    }

    internal (string Source, string? Columns) EmitNode(RelNode node) => node switch
    {
        ScanNode scan => EmitScan(scan),
        FilterNode filter => EmitFilter(filter),
        ProjectNode project => EmitProject(project),
        ExtendNode extend => EmitExtend(extend),
        AggregateNode agg => EmitAggregate(agg),
        SortNode sort => EmitSort(sort),
        LimitNode limit => EmitLimit(limit),
        SampleNode sample => EmitSample(sample),
        DistinctNode dist => EmitDistinct(dist),
        JoinNode join => _joinEmitter.EmitJoin(join),
        SingletonRowNode single => EmitSingleton(single),
        LetBindingNode let_ => EmitLet(let_),
        _ => throw new NotSupportedException($"Unsupported RelNode type: {node.GetType().Name}")
    };

    #region Scan

    private (string Source, string? Columns) EmitScan(ScanNode scan) => ($"golden.{DuckDbSqlText.EscapeIdent(scan.ViewName)}", null);

    #endregion Scan

    #region Filter

    private (string Source, string? Columns) EmitFilter(FilterNode filter)
    {
        var source = StageFrom(filter.Input);
        var stage = _context.Stages.NextStage();
        var pred = _scalarEmitter.EmitScalar(filter.Predicate);
        _context.Stages.AddStage(stage, $"SELECT * FROM {source} WHERE {pred}");
        return (stage, null);
    }

    #endregion Filter

    #region Project

    private (string Source, string? Columns) EmitProject(ProjectNode project)
    {
        var source = StageFrom(project.Input);
        var cols = string.Join(", ", project.Projections.Select(_scalarEmitter.EmitProjection));
        return (source, cols);
    }

    #endregion Project

    #region Extend

    private (string Source, string? Columns) EmitExtend(ExtendNode extend)
    {
        var extensions = string.Join(", ", extend.Extensions.Select(_scalarEmitter.EmitProjection));
        if (extend.Input is FilterNode filter)
        {
            var (filterSource, filterColumns) = EmitNode(filter.Input);
            var src = filterColumns is null ? filterSource : $"(SELECT {filterColumns} FROM {filterSource})";
            var predicate = _scalarEmitter.EmitScalar(filter.Predicate);
            var next = _context.Stages.NextStage();
            _context.Stages.AddStage(next, $"SELECT *, {extensions} FROM {src} WHERE {predicate}");
            return (next, null);
        }

        var source = StageFrom(extend.Input);
        var stage = _context.Stages.NextStage();
        _context.Stages.AddStage(stage, $"SELECT *, {extensions} FROM {source}");
        return (stage, null);
    }

    #endregion Extend

    #region Aggregate

    private (string Source, string? Columns) EmitAggregate(AggregateNode agg)
    {
        var source = StageFrom(agg.Input);
        var groupCols = agg.GroupBy.Select(_scalarEmitter.EmitScalar).ToList();
        List<string> aggCols;
        _context.InAggregateProjection = true;
        try
        {
            aggCols = agg.Aggregates.Select(_scalarEmitter.EmitProjection).ToList();
        }
        finally
        {
            _context.InAggregateProjection = false;
        }

        var allCols = new List<string>();
        allCols.AddRange(groupCols);
        allCols.AddRange(aggCols);

        var stage = _context.Stages.NextStage();
        var sql = $"SELECT {string.Join(", ", allCols)} FROM {source}";
        if (groupCols.Count > 0)
        {
            sql += $" GROUP BY {string.Join(", ", groupCols)}";
        }

        _context.Stages.AddStage(stage, sql);
        return (stage, null);
    }

    #endregion Aggregate

    #region Sort

    private (string Source, string? Columns) EmitSort(SortNode sort)
    {
        var source = StageFrom(sort.Input);
        var orders = string.Join(", ", sort.Sorts.Select(s => _scalarEmitter.EmitTabularSortExpr(s, sort.Input)));
        var stage = _context.Stages.NextStage();
        _context.Stages.AddStage(stage, $"SELECT * FROM {source} ORDER BY {orders}");
        return (stage, null);
    }

    #endregion Sort

    #region Distinct

    private (string Source, string? Columns) EmitDistinct(DistinctNode dist)
    {
        var source = StageFrom(dist.Input);
        var cols = string.Join(", ", dist.Projections.Select(_scalarEmitter.EmitProjection));
        var stage = _context.Stages.NextStage();
        _context.Stages.AddStage(stage, $"SELECT DISTINCT {cols} FROM {source}");
        return (stage, null);
    }

    #endregion Distinct

    #region Limit

    private (string Source, string? Columns) EmitLimit(LimitNode limit)
    {
        // Sort/take fusion: a SortNode directly beneath a LimitNode is a single
        // top-k operation. Emit ORDER BY and LIMIT in one query block rather than
        // two stages — this also avoids depending on ordering surviving an
        // intermediate CTE boundary.
        if (limit.Input is SortNode sort)
        {
            var sortSource = StageFrom(sort.Input);
            var orders = string.Join(", ", sort.Sorts.Select(s => _scalarEmitter.EmitTabularSortExpr(s, sort.Input)));
            var fused = _context.Stages.NextStage();
            _context.Stages.AddStage(fused, $"SELECT * FROM {sortSource} ORDER BY {orders} LIMIT {limit.Count}");
            return (fused, null);
        }

        var source = StageFrom(limit.Input);
        var stage = _context.Stages.NextStage();
        _context.Stages.AddStage(stage, $"SELECT * FROM {source} LIMIT {limit.Count}");
        return (stage, null);
    }

    private (string Source, string? Columns) EmitSample(SampleNode sample)
    {
        var source = StageFrom(sample.Input);
        var stage = _context.Stages.NextStage();
        _context.Stages.AddStage(stage, $"SELECT * FROM {source} USING SAMPLE reservoir({sample.Count} ROWS)");
        return (stage, null);
    }

    private bool TryRenderSampleDistinct(RelNode node, out string sql)
    {
        sql = string.Empty;
        if (node is not SampleNode { Input: DistinctNode distinct } sample
            || distinct.Projections.Count != 1
            || !string.Equals(distinct.Projections[0].Alias, "sample_distinct_value", StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryRenderSampleDistinctSource(distinct.Input, out var source))
        {
            return false;
        }

        var projection = _scalarEmitter.EmitScalar(distinct.Projections[0].Expression);
        sql = $"SELECT DISTINCT {projection} FROM {source} LIMIT {sample.Count}";
        return true;
    }

    private bool TryRenderSampleDistinctSource(RelNode node, out string source)
    {
        switch (node)
        {
            case ScanNode scan:
                source = $"golden.{DuckDbSqlText.EscapeIdent(scan.ViewName)}";
                return true;

            case FilterNode { Input: ScanNode scan } filter:
                source = $"golden.{DuckDbSqlText.EscapeIdent(scan.ViewName)} WHERE {_scalarEmitter.EmitScalar(filter.Predicate)}";
                return true;

            default:
                source = string.Empty;
                return false;
        }
    }

    #endregion Limit

    private (string Source, string? Columns) EmitSingleton(SingletonRowNode _)
    {
        var stage = _context.Stages.NextStage();
        _context.Stages.AddStage(stage, "SELECT 1 AS __seed");
        return (stage, "__seed");
    }

    #region Let

    private (string Source, string? Columns) EmitLet(LetBindingNode let_)
    {
        if (let_.ScalarValue is not null)
        {
            // Scalar let: emit the value expression and register it for substitution.
            // Any subsequent ColumnRef("name") in the body will resolve to this SQL.
            var valueExpr = _scalarEmitter.EmitScalar(let_.ScalarValue);
            _context.ScalarBindings[let_.Name] = valueExpr;
        }

        if (let_.TabularValue is not null)
        {
            // Tabular let: emit the subquery as a named CTE.
            var (tabSource, tabCols) = EmitNode(let_.TabularValue);
            var letName = DuckDbSqlText.EscapeIdent(let_.Name);
            _context.Stages.AddStage(letName, $"SELECT {tabCols ?? "*"} FROM {tabSource}");
        }

        return EmitNode(let_.Body);
    }

    #endregion Let
}
