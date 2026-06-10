using DeltaZulu.Workbench.Application.Abstractions;

namespace DeltaZulu.Workbench.HuntingAdapter;

/// <summary>
/// Adapts a reusable Hunting.Core parser/translator boundary to Workbench's query validation seam.
/// The adapter deliberately depends only on the application validation contract and a small parser port
/// so it can be wired to Hunting.Core without referencing Hunting.Web or executing runtime queries.
/// </summary>
public sealed class HuntingCoreQuerySyntaxValidator(IHuntingCoreQueryParser parser) : IQuerySyntaxValidator
{
    private readonly IHuntingCoreQueryParser _parser = parser ?? throw new ArgumentNullException(nameof(parser));

    /// <inheritdoc />
    public QuerySyntaxValidationResult Validate(QuerySyntaxValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = _parser.Parse(new HuntingCoreQueryParseRequest(
            request.LogicalPath,
            request.Content,
            request.DetectionSlug));

        if (result.IsValid)
        {
            return QuerySyntaxValidationResult.Pass();
        }

        var diagnostics = result.Diagnostics
            .Select(d => new QuerySyntaxDiagnostic(d.Message, d.Line, d.Column))
            .ToArray();

        return diagnostics.Length == 0
            ? QuerySyntaxValidationResult.Fail(new QuerySyntaxDiagnostic("Hunting.Core rejected the query without diagnostics."))
            : QuerySyntaxValidationResult.Fail(diagnostics);
    }
}