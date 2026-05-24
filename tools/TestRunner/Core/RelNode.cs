namespace Hunting.Core.QueryModel;

// ─── Relational nodes (query plan) ─────────────────────────────────────

public abstract record RelNode;

public sealed record ScanNode(string ViewName) : RelNode;

public sealed record FilterNode(RelNode Input, ScalarExpr Predicate) : RelNode;

public sealed record ProjectNode(RelNode Input, IReadOnlyList<ProjectionExpr> Projections) : RelNode;

public sealed record ExtendNode(RelNode Input, IReadOnlyList<ProjectionExpr> Extensions) : RelNode;

public sealed record AggregateNode(
    RelNode Input,
    IReadOnlyList<ProjectionExpr> Aggregates,
    IReadOnlyList<ScalarExpr> GroupBy) : RelNode;

public sealed record SortNode(RelNode Input, IReadOnlyList<SortExpr> Sorts) : RelNode;

public sealed record LimitNode(RelNode Input, int Count) : RelNode;

/// <summary>
/// Represents KQL 'distinct col1, col2' — emits SELECT DISTINCT.
/// The column list follows the argument order (unlike project-keep which uses schema order).
/// </summary>
public sealed record DistinctNode(
    RelNode Input,
    IReadOnlyList<ProjectionExpr> Projections) : RelNode;

public sealed record JoinNode(
    RelNode Left,
    RelNode Right,
    JoinKind Kind,
    ScalarExpr OnPredicate) : RelNode;

public sealed record LetBindingNode(
    string Name,
    RelNode? TabularValue,
    ScalarExpr? ScalarValue,
    RelNode Body) : RelNode;

// ─── Scalar expressions ─────────────────────────────────────────────────

public abstract record ScalarExpr;

public sealed record ColumnRef(string Name) : ScalarExpr;

public sealed record LiteralScalar(object? Value, LiteralKind Kind) : ScalarExpr;

public sealed record BinaryScalar(ScalarExpr Left, ScalarBinaryOp Op, ScalarExpr Right) : ScalarExpr;

public sealed record UnaryScalar(ScalarUnaryOp Op, ScalarExpr Operand) : ScalarExpr;

public sealed record FunctionCall(string Name, IReadOnlyList<ScalarExpr> Args) : ScalarExpr;

public sealed record CaseScalar(
    IReadOnlyList<(ScalarExpr When, ScalarExpr Then)> Branches,
    ScalarExpr Else) : ScalarExpr;

public sealed record StarExpr() : ScalarExpr;

/// <summary>
/// A scalar expression that wraps a function call with an OVER (...) window specification.
/// Produced by KQL serialize + prev()/next()/row_number()/row_cumsum().
/// Emitted as: function(...) OVER (PARTITION BY ... ORDER BY ... frame)
/// </summary>
public sealed record WindowScalarExpr(
    string FunctionName,
    IReadOnlyList<ScalarExpr> Args,
    WindowSpec Window) : ScalarExpr;

// ─── Window specification ───────────────────────────────────────────────

/// <summary>
/// SQL window specification: OVER (PARTITION BY ... ORDER BY ... frame).
/// Used by WindowScalarExpr for serialize-family operators and by
/// stream windowing patterns (tumbling, hopping, sliding, session).
/// </summary>
public sealed record WindowSpec(
    IReadOnlyList<ScalarExpr> PartitionBy,
    IReadOnlyList<SortExpr> OrderBy,
    WindowFrame? Frame = null);

/// <summary>
/// Window frame: ROWS/RANGE BETWEEN start AND end.
/// </summary>
public sealed record WindowFrame(
    WindowFrameType Type,
    WindowBound Start,
    WindowBound End);

public enum WindowFrameType { Rows, Range }

/// <summary>
/// A window frame bound: UNBOUNDED PRECEDING, N PRECEDING, CURRENT ROW,
/// N FOLLOWING, UNBOUNDED FOLLOWING, or INTERVAL-based RANGE bound.
/// </summary>
public sealed record WindowBound(WindowBoundKind Kind, ScalarExpr? Offset = null);

public enum WindowBoundKind
{
    UnboundedPreceding,
    Preceding,          // offset required (integer for ROWS, interval for RANGE)
    CurrentRow,
    Following,          // offset required
    UnboundedFollowing
}

// ─── Supporting types ───────────────────────────────────────────────────

public sealed record ProjectionExpr(string Alias, ScalarExpr Expression);

public sealed record SortExpr(ScalarExpr Expression, SortDirection Direction, NullOrder Nulls = NullOrder.Default);

public enum JoinKind { Inner, LeftOuter, LeftSemi, LeftAnti }

public enum SortDirection { Asc, Desc }

public enum NullOrder { Default, First, Last }

public enum LiteralKind { String, Long, Int, Real, Bool, DateTime, Null, Timespan }

public enum ScalarBinaryOp
{
    Eq, Neq, Lt, Lte, Gt, Gte,
    And, Or,
    Add, Sub, Mul, Div, Mod,
    Contains, NotContains,               // case-insensitive (KQL default)
    ContainsCs, NotContainsCs,           // case-sensitive (_cs variants)
    Has, NotHas,                         // case-insensitive word-boundary match (approximated via regex \b)
    HasCs, NotHasCs,                     // case-sensitive word-boundary match
    StartsWith, NotStartsWith,           // case-insensitive
    StartsWithCs, NotStartsWithCs,       // case-sensitive
    EndsWith, NotEndsWith,               // case-insensitive
    EndsWithCs, NotEndsWithCs,           // case-sensitive
    HasPrefix, NotHasPrefix,             // case-insensitive word-boundary prefix
    HasPrefixCs, NotHasPrefixCs,         // case-sensitive word-boundary prefix
    HasSuffix, NotHasSuffix,             // case-insensitive word-boundary suffix
    HasSuffixCs, NotHasSuffixCs,         // case-sensitive word-boundary suffix
    MatchesRegex, NotMatchesRegex,       // regex match (NotMatchesRegex = NOT(MatchesRegex))
    In, NotIn
}

public enum ScalarUnaryOp
{
    Not, Negate
}
