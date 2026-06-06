using Workbench.Application.Abstractions;

namespace Workbench.Validation.Checks;

/// <summary>
/// Minimal deterministic validator used until a product parser adapter is registered.
/// It intentionally validates only the invariant that query artifacts contain text.
/// </summary>
public sealed class NonEmptyQuerySyntaxValidator : IQuerySyntaxValidator
{
    public QuerySyntaxValidationResult Validate(QuerySyntaxValidationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return QuerySyntaxValidationResult.Fail(
                new QuerySyntaxDiagnostic("query content is empty."));
        }

        return QuerySyntaxValidationResult.Pass();
    }
}
