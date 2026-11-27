using DeltaZulu.Platform.Application.Analytics.Translation;
using DeltaZulu.Platform.Domain.Analytics.Catalog;
using DeltaZulu.Platform.Domain.Analytics.Policy;
using DeltaZulu.Platform.Domain.Analytics.QueryModel;

namespace DeltaZulu.Platform.Application.Analytics.Nrt;

/// <summary>Compiles a bound KQL plan for the initial stateless stream execution surface.</summary>
public sealed class LocalStreamNrtCompiler(ApprovedViewCatalog catalog)
{
    public (RelNode? Plan, IReadOnlyList<string> Errors) Compile(string kql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kql);
        var diagnostics = new DiagnosticBag();
        var plan = new KustoQueryCompiler(catalog).Compile(kql, diagnostics);
        if (plan is null || diagnostics.HasErrors)
            return (null, diagnostics.All.Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.Message).DefaultIfEmpty("KQL compilation produced no output.").ToArray());

        var errors = new List<string>();
        Validate(plan, errors);
        return errors.Count == 0 ? (plan, errors) : (null, errors);
    }

    private static void Validate(RelNode node, List<string> errors)
    {
        switch (node)
        {
            case ScanNode:
                break;
            case FilterNode filter:
                Validate(filter.Input, errors);
                ValidatePredicate(filter.Predicate, errors);
                break;
            case ProjectNode project:
                Validate(project.Input, errors);
                foreach (var projection in project.Projections)
                    if (projection.Expression is not ColumnRef { Qualifier: null })
                        errors.Add("Stream projection currently supports unqualified columns only.");
                break;
            default:
                errors.Add($"Unsupported stream operator: {node.GetType().Name}.");
                break;
        }
    }

    private static void ValidatePredicate(ScalarExpr expression, List<string> errors)
    {
        switch (expression)
        {
            case BinaryScalar { Op: ScalarBinaryOp.And or ScalarBinaryOp.Or } binary:
                ValidatePredicate(binary.Left, errors);
                ValidatePredicate(binary.Right, errors);
                break;
            case BinaryScalar { Op: ScalarBinaryOp.Eq or ScalarBinaryOp.Neq,
                Left: ColumnRef { Qualifier: null },
                Right: LiteralScalar { Kind: LiteralKind.String or LiteralKind.Bool or LiteralKind.Int or LiteralKind.Long, Value: not null } }:
                break;
            default:
                errors.Add($"Unsupported stream predicate: {expression.GetType().Name}.");
                break;
        }
    }
}
