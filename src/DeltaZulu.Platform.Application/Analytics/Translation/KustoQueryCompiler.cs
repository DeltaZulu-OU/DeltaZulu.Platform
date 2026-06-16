using DeltaZulu.Platform.Domain.Analytics.Catalog;
using DeltaZulu.Platform.Domain.Analytics.Compilation;
using DeltaZulu.Platform.Domain.Analytics.Policy;
using DeltaZulu.Platform.Domain.Analytics.QueryModel;

namespace DeltaZulu.Platform.Application.Analytics.Translation;

public sealed class KustoQueryCompiler : IQueryCompiler
{
    private readonly ApprovedViewCatalog _approvedViews;

    public KustoQueryCompiler(ApprovedViewCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _approvedViews = catalog;
    }

    public long CatalogVersion => _approvedViews.CatalogVersion;

    public RelNode? Compile(string queryText, DiagnosticBag diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        return new KustoToRelational(_approvedViews, diagnostics).Translate(queryText);
    }
}
