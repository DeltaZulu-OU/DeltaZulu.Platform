using DeltaZulu.Workbench.Application.Abstractions;

namespace DeltaZulu.Workbench.Validation.Checks;

/// <summary>
/// Minimal deterministic validator used until a product parser adapter is registered.
/// It intentionally validates only the invariant that query artifacts contain text.
/// </summary>
public sealed class NonEmptyQuerySyntaxValidator : IQuerySyntaxValidator
{
    public QuerySyntaxValidationResult Validate(QuerySyntaxValidationRequest request)
    {
        return string.IsNullOrWhiteSpace(request.Content)
            ? QuerySyntaxValidationResult.Fail(
                new QuerySyntaxDiagnostic("query content is empty."))
            : QuerySyntaxValidationResult.Pass();
    }
}