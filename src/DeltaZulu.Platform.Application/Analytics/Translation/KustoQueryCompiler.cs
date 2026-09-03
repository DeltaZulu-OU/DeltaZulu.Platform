using DeltaZulu.Kql.Compilation;
using DeltaZulu.Kql.Relational;
using DeltaZulu.Platform.Domain.Analytics.Catalog;
using DeltaZulu.Platform.Domain.Analytics.Compilation;
using DeltaZulu.Platform.Domain.Analytics.Policy;

namespace DeltaZulu.Platform.Application.Analytics.Translation;

public sealed class KustoQueryCompiler : IQueryCompiler
{
    private static readonly KqlRelationalCompiler Compiler = new();

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
        var result = Compiler.Compile(queryText, new ApprovedViewCatalogSchemaAdapter(_approvedViews));
        KqlDiagnosticAdapter.CopyInto(result.Diagnostics, diagnostics);
        return result.Root;
    }
}
