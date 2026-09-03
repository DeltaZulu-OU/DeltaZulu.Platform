using DeltaZulu.Kql.Relational;

namespace DeltaZulu.Platform.Data.DuckDb.Sql;

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

        // DuckDB has no directional RIGHT SEMI/ANTI JOIN syntax (only bare SEMI/ANTI JOIN,
        // which keeps rows from whichever table is on the FROM side). KQL's rightsemi/rightanti
        // keep matching rows from the right input, so those two kinds swap which physical
        // source sits in the FROM position instead of using a (nonexistent) RIGHT-qualified
        // keyword. The join-side aliases stay bound to their conceptual join.Left/join.Right
        // regardless of physical FROM/JOIN position, so the predicate emitted above is
        // unaffected by the swap.
        var joinKind = join.Kind switch {
            JoinKind.Inner => "INNER JOIN",
            JoinKind.LeftOuter => "LEFT JOIN",
            JoinKind.RightOuter => "RIGHT JOIN",
            JoinKind.FullOuter => "FULL OUTER JOIN",
            JoinKind.LeftSemi => "SEMI JOIN",
            JoinKind.LeftAnti => "ANTI JOIN",
            JoinKind.RightSemi => "SEMI JOIN",
            JoinKind.RightAnti => "ANTI JOIN",
            _ => throw new NotSupportedException($"Unsupported join kind: {join.Kind}")
        };

        var swapSides = join.Kind is JoinKind.RightSemi or JoinKind.RightAnti;
        var (fromSource, fromAlias, joinSource, joinAlias) = swapSides
            ? (rightSource, rightAlias, leftSource, leftAlias)
            : (leftSource, leftAlias, rightSource, rightAlias);

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
        _context.Stages.AddStage(stage, $"SELECT {selectList} FROM {fromSource} AS {fromAlias} {joinKind} {joinSource} AS {joinAlias} ON {pred}");
        return (stage, null);
    }

    private string StageFrom(RelNode node) =>
        (_stageFrom ?? throw new InvalidOperationException("Relational emitter callbacks are not bound."))(node);
}