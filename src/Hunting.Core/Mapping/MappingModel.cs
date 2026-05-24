namespace Hunting.Core.Mapping;

using Schema;

// ─── Expression tree for parser view mappings ───────────────────────────

public abstract record ExprDef;

public sealed record ColumnExpr(string Name) : ExprDef;
public sealed record LiteralExpr(object? Value) : ExprDef;
public sealed record JsonTextExpr(ExprDef JsonColumn, string Path) : ExprDef;
public sealed record RegexExtractExpr(ExprDef Input, string Pattern, int Group) : ExprDef;
public sealed record CastExpr(ExprDef Input, DuckDbType TargetType) : ExprDef;
public sealed record CaseExpr(IReadOnlyList<CaseBranch> Branches, ExprDef Else) : ExprDef;
public sealed record FunctionExpr(string Name, IReadOnlyList<ExprDef> Args) : ExprDef;
public sealed record BinaryExpr(ExprDef Left, BinaryOp Op, ExprDef Right) : ExprDef;

public sealed record CaseBranch(ExprDef When, ExprDef Then);

public enum BinaryOp
{
    Eq, Neq, Lt, Lte, Gt, Gte, And, Or
}

// ─── Projection and query model ─────────────────────────────────────────

public sealed record ProjectionDef(string TargetColumn, ExprDef Expression);

public sealed record MappingQueryDef(
    string SourceObject,
    ExprDef? Filter,
    IReadOnlyList<ProjectionDef> Projections);

// ─── Builder helpers for concise mapping authoring ──────────────────────

public static class MapDsl
{
    public static ColumnExpr Col(string name) => new(name);
    public static LiteralExpr Lit(object? value) => new(value);
    public static JsonTextExpr JsonText(ExprDef col, string path) => new(col, path);
    public static RegexExtractExpr RegexExtract(ExprDef input, string pattern, int group) => new(input, pattern, group);
    public static CastExpr Cast(ExprDef input, DuckDbType type) => new(input, type);
    public static FunctionExpr Fn(string name, params ExprDef[] args) => new(name, args);
    public static ProjectionDef Map(string target, ExprDef expr) => new(target, expr);

    public static BinaryExpr Eq(ExprDef left, ExprDef right) => new(left, BinaryOp.Eq, right);
    public static BinaryExpr And(ExprDef left, ExprDef right) => new(left, BinaryOp.And, right);
    public static BinaryExpr Or(ExprDef left, ExprDef right) => new(left, BinaryOp.Or, right);
}
