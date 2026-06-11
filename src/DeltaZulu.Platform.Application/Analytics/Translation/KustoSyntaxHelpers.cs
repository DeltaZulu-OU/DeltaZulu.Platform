
using Kusto.Language.Syntax;

namespace DeltaZulu.Platform.Application.Analytics.Translation;
/// <summary>Adapts Kusto.Language SDK list and path syntax shapes for translators.</summary>
internal static class KustoSyntaxHelpers
{
    public static List<string> GetPathParts(PathExpression path)
        => path.GetDescendants<NameReference>().Select(n => n.SimpleName).ToList();

    public static IReadOnlyList<Expression> ExtractDirectExpressionListItems(SyntaxElement listNode)
    {
        var result = new List<Expression>();
        for (var i = 0; i < listNode.ChildCount; i++)
        {
            var child = listNode.GetChild(i);
            switch (child)
            {
                case Expression expr: result.Add(expr); break;
                case SeparatedElement<Expression> separated: result.Add(separated.Element); break;
                case SyntaxList<SeparatedElement<Expression>> list:
                    foreach (var separated in list) { result.Add(separated.Element); }
                    break;
            }
        }
        return result;
    }

    public static Expression? UnwrapSeparated(SyntaxNode node) => node switch
    {
        SeparatedElement<Expression> separated => separated.Element,
        Expression expr => expr,
        _ => null
    };
}