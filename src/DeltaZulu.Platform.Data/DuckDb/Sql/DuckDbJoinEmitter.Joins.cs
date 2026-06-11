namespace DeltaZulu.Platform.Data.DuckDb.Sql;

using QueryModel;

internal sealed partial class DuckDbJoinEmitter
{
    private readonly DuckDbEmitterContext _context;
    private readonly DuckDbScalarEmitter _scalarEmitter;
    private Func<RelNode, (string Source, string? Columns)>? _emitNode;
    private Func<RelNode, string>? _stageFrom;

    internal DuckDbJoinEmitter(DuckDbEmitterContext context, DuckDbScalarEmitter scalarEmitter)
    {
        _context = context;
        _scalarEmitter = scalarEmitter;
    }

    internal void BindRelationalEmitter(
        Func<RelNode, (string Source, string? Columns)> emitNode,
        Func<RelNode, string> stageFrom)
    {
        _emitNode = emitNode;
        _stageFrom = stageFrom;
    }

    internal (string Source, string? Columns) EmitJoin(JoinNode join)
    {
        // Emit both inputs before binding the join-side aliases — a nested join
        // sets these same fields, so the predicate must be emitted only after all
        // child emission is complete.
        var leftSource = StageFrom(join.Left);
        var rightSource = StageFrom(join.Right);

        // Explicit aliases disambiguate self-joins and survive CTE inlining (which
        // rewrites the stage names in the FROM clause but leaves the aliases).
        const string leftAlias = "__join_left";
        const string rightAlias = "__join_right";

        _context.JoinLeftAlias = leftAlias;
        _context.JoinRightAlias = rightAlias;
        string pred;
        try
        {
            pred = _scalarEmitter.EmitScalar(join.OnPredicate);
        }
        finally
        {
            _context.JoinLeftAlias = null;
            _context.JoinRightAlias = null;
        }

        var joinKind = join.Kind switch
        {
            JoinKind.Inner => "INNER JOIN",
            JoinKind.LeftOuter => "LEFT JOIN",
            JoinKind.RightOuter => "RIGHT JOIN",
            JoinKind.FullOuter => "FULL OUTER JOIN",
            JoinKind.LeftSemi => "SEMI JOIN",
            JoinKind.LeftAnti => "ANTI JOIN",
            JoinKind.RightSemi => "RIGHT SEMI JOIN",
            JoinKind.RightAnti => "RIGHT ANTI JOIN",
            _ => throw new NotSupportedException($"Unsupported join kind: {join.Kind}")
        };

        var selectList = "*";
        if (join is { Flavor: JoinFlavor.Lookup, Kind: JoinKind.LeftOuter }
            && TryBuildLookupJoinProjection(join.Right, join.OnPredicate, out var rightPayloadCols))
        {
            selectList = $"{leftAlias}.*";
            if (rightPayloadCols.Count > 0)
            {
                selectList += ", " + string.Join(", ", rightPayloadCols.Select(c => $"{rightAlias}.{DuckDbSqlText.EscapeIdent(c)}"));
            }
        }

        var stage = _context.Stages.NextStage();
        _context.Stages.AddStage(stage, $"SELECT {selectList} FROM {leftSource} AS {leftAlias} {joinKind} {rightSource} AS {rightAlias} ON {pred}");
        return (stage, null);
    }

    private string StageFrom(RelNode node) =>
        (_stageFrom ?? throw new InvalidOperationException("Relational emitter callbacks are not bound."))(node);
}