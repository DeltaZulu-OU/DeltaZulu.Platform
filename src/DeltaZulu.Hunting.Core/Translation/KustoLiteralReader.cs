namespace DeltaZulu.Hunting.Core.Translation;

using Kusto.Language.Syntax;
using Policy;

/// <summary>Reads operator literals while preserving translator diagnostics.</summary>
internal static class KustoLiteralReader
{
    public static int GetIntLiteral(DiagnosticBag diagnostics, Expression expr)
    {
        if (expr is LiteralExpression lit)
        {
            if (lit.LiteralValue is long longValue) { return (int)longValue; }
            if (lit.LiteralValue is int intValue) { return intValue; }
            if (lit.LiteralValue is double doubleValue) { return (int)doubleValue; }
        }
        if (expr is SimpleNamedExpression named)
        {
            return GetIntLiteral(diagnostics, named.Expression);
        }

        diagnostics.AddError(
            DiagnosticPhase.Translate,
            "take/top count must be a positive integer literal. Variable or expression counts are not supported in MVP.",
            expr.ToString(),
            expr.TextStart,
            expr.Width);
        return -1;
    }
}