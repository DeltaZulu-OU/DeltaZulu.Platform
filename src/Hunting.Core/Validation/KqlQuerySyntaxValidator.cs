namespace Hunting.Core.Validation;

using Hunting.Core.Catalog;
using Hunting.Core.Policy;
using Hunting.Core.Translation;

/// <summary>
/// Reusable Hunting.Core-backed KQL validation adapter. It exercises the same
/// catalog policy and translator path as runtime query execution, but stops at
/// the RelNode boundary so Workbench and other hosts can validate query content
/// without referencing Hunting.Web or executing generated DuckDB SQL.
/// </summary>
public sealed class KqlQuerySyntaxValidator : IQuerySyntaxValidator
{
    private readonly ApprovedViewCatalog _catalog;

    public KqlQuerySyntaxValidator(ApprovedViewCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public QuerySyntaxValidationResult Validate(string queryText)
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return new QuerySyntaxValidationResult(
                false,
                [new QueryDiagnostic(
                    DiagnosticSeverity.Error,
                    DiagnosticPhase.Parse,
                    QueryDiagnosticCodes.Unspecified,
                    "Query text is required.")]);
        }

        var diagnostics = new DiagnosticBag();
        var translator = new KustoToRelational(_catalog, diagnostics);
        _ = translator.Translate(queryText);

        return QuerySyntaxValidationResult.FromDiagnostics(diagnostics.All);
    }
}
