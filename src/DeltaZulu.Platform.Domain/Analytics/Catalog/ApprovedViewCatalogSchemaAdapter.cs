using DeltaZulu.Kql.Compilation;
using DeltaZulu.Platform.Domain.Analytics.Schema;
using Kusto.Language;

namespace DeltaZulu.Platform.Domain.Analytics.Catalog;

/// <summary>
/// Adapts Platform's <see cref="ApprovedViewCatalog"/> to the generic
/// DeltaZulu.Kql schema contract (<see cref="IKqlSchemaCatalog"/>) the shared
/// KQL relational compiler needs. <see cref="ApprovedViewCatalog"/> remains the
/// sole owner of medallion/approval policy, canonical view registration, and
/// GlobalState caching -- this type only reshapes what it already exposes.
/// </summary>
public sealed class ApprovedViewCatalogSchemaAdapter : IKqlSchemaCatalog
{
    private readonly ApprovedViewCatalog _catalog;

    public ApprovedViewCatalogSchemaAdapter(ApprovedViewCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    public long Version => _catalog.CatalogVersion;

    public IEnumerable<KqlTableSchema> Tables => _catalog.Views.Values.Select(ToTableSchema);

    public bool TryGetTable(string name, out KqlTableSchema schema)
    {
        var view = _catalog.Resolve(name);
        if (view is null)
        {
            schema = null!;
            return false;
        }

        schema = ToTableSchema(view);
        return true;
    }

    // ApprovedViewCatalog already caches its GlobalState under a lock, keyed on
    // its own version and invalidated on registration -- delegate straight to it
    // rather than building a second, redundant cache here.
    public GlobalState BuildGlobalState() => _catalog.BuildGlobalState();

    // The KQL type is declared on ColumnDef, never inferred from a CLR value;
    // this only carries that declared name into the shared schema contract.
    private static KqlTableSchema ToTableSchema(CanonicalViewDef view) => new(
        view.Name,
        view.Columns.Select(c => new KqlColumnSchema(c.Name, c.KustoType.ToKustoName())).ToArray());
}
