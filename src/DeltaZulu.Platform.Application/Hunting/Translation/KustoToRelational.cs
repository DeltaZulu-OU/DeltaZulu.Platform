
using DeltaZulu.Platform.Application.Hunting.Catalog;
using DeltaZulu.Platform.Domain.Hunting.Policy;
using DeltaZulu.Platform.Domain.Hunting.QueryModel;

namespace DeltaZulu.Platform.Application.Hunting.Translation;
/// <summary>
/// Backward-compatible public entry point for KQL-to-<see cref="RelNode"/> translation.
/// Translation behavior lives in the internal <see cref="KustoQueryTranslator"/> facade so
/// this adapter can remain stable until a deliberate public API breaking change is acceptable.
/// </summary>
public sealed class KustoToRelational
{
    private readonly KustoQueryTranslator _translator;

    public KustoToRelational(ApprovedViewCatalog catalog, DiagnosticBag diagnostics)
    {
        _translator = new KustoQueryTranslator(catalog, diagnostics);
    }

    public RelNode? Translate(string kql) => _translator.Translate(kql);
}