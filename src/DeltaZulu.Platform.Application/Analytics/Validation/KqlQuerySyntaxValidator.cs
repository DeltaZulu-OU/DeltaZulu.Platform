using DeltaZulu.Platform.Application.Analytics.Translation;
using DeltaZulu.Platform.Domain.Analytics.Catalog;
using DeltaZulu.Platform.Domain.Analytics.Policy;
using DeltaZulu.Platform.Domain.Governance.Contracts;

namespace DeltaZulu.Platform.Application.Analytics.Validation;

/// <summary>
/// KQL validation adapter that exercises the catalog policy and translator path used by runtime
/// query execution, stopping at the RelNode boundary so callers can validate content without
/// opening DuckDB connections or depending on web/runtime composition.
/// </summary>
public sealed class KqlQuerySyntaxValidator : IQuerySyntaxValidator
{
    private readonly ApprovedViewCatalog _catalog;

    public KqlQuerySyntaxValidator(ApprovedViewCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public QuerySyntaxValidationResult Validate(QuerySyntaxValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return QuerySyntaxValidationResult.Fail(
                new QuerySyntaxDiagnostic("Query text is required."));
        }

        var diagnostics = new DiagnosticBag();
        var translator = new KustoToRelational(_catalog, diagnostics);
        _ = translator.Translate(request.Content);

        var errors = diagnostics.All
            .Where(d => d.IsError)
            .Select(d => new QuerySyntaxDiagnostic(d.Message))
            .ToArray();

        return errors.Length == 0
            ? QuerySyntaxValidationResult.Pass()
            : QuerySyntaxValidationResult.Fail(errors);
    }
}
