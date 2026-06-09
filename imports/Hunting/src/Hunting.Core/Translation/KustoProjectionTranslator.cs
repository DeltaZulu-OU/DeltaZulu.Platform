namespace Hunting.Core.Translation;

using Kusto.Language.Syntax;
using QueryModel;

/// <summary>Translates projection aliases and ordered expressions using scalar translation.</summary>
internal sealed class KustoProjectionTranslator
{
    private readonly Func<Expression?, ScalarExpr> _translateScalarExpr;

    public KustoProjectionTranslator(Func<Expression?, ScalarExpr> translateScalarExpr)
    {
        _translateScalarExpr = translateScalarExpr;
    }

    public ProjectionExpr TranslateProjectionExpr(Expression? expr)
    {
        ArgumentNullException.ThrowIfNull(expr);
        if (expr is SimpleNamedExpression named)
        {
            return new ProjectionExpr(named.Name.SimpleName, _translateScalarExpr(named.Expression));
        }

        var scalar = _translateScalarExpr(expr);
        var name = scalar switch
        {
            ColumnRef column => column.Name,
            _ when expr is FunctionCallExpression function => function.Name.SimpleName + "_",
            _ => expr.ToString().Replace("\"", "").Trim()
        };
        return new ProjectionExpr(name, scalar);
    }

    public SortExpr TranslateSortExpr(Expression? expr)
    {
        ArgumentNullException.ThrowIfNull(expr);
        var direction = SortDirection.Desc;
        var columnExpr = expr;
        if (expr is OrderedExpression ordered)
        {
            columnExpr = ordered.Expression;
            var directionToken = ordered.Ordering?.GetFirstToken();
            if (directionToken is not null)
            {
                direction = directionToken.Kind == SyntaxKind.AscKeyword ? SortDirection.Asc : SortDirection.Desc;
            }
        }
        return new SortExpr(_translateScalarExpr(columnExpr), direction);
    }
}
