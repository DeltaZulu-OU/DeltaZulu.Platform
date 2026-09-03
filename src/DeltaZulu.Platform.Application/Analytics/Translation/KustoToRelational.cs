using DeltaZulu.Kql.Compilation;
using DeltaZulu.Kql.Relational;
using DeltaZulu.Platform.Domain.Analytics.Catalog;
using DeltaZulu.Platform.Domain.Analytics.Policy;

namespace DeltaZulu.Platform.Application.Analytics.Translation;

/// <summary>
/// Backward-compatible public entry point for KQL-to-<see cref="RelNode"/> translation.
/// Now a thin shim over the shared DeltaZulu.Kql compiler -- kept only for its
/// existing call sites during migration and scheduled for removal once they
/// move to <see cref="KqlRelationalCompiler"/> directly.
/// </summary>
public sealed class KustoToRelational
{
    private static readonly KqlRelationalCompiler Compiler = new();

    private readonly ApprovedViewCatalog _catalog;
    private readonly DiagnosticBag _diagnostics;

    public KustoToRelational(ApprovedViewCatalog catalog, DiagnosticBag diagnostics)
    {
        _catalog = catalog;
        _diagnostics = diagnostics;
    }

    public RelNode? Translate(string kql)
    {
        var result = Compiler.Compile(kql, new ApprovedViewCatalogSchemaAdapter(_catalog));
        KqlDiagnosticAdapter.CopyInto(result.Diagnostics, _diagnostics);
        return result.Root;
    }
}
