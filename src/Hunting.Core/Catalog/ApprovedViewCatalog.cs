namespace Hunting.Core.Catalog;

using Kusto.Language;
using Kusto.Language.Symbols;
using Schema;

/// <summary>
/// Builds and maintains the approved public hunting view catalog.
/// Produces the Kusto.Language GlobalState used for semantic analysis
/// and the set of approved view names for policy enforcement.
/// </summary>
public sealed class ApprovedViewCatalog
{
    private readonly Dictionary<string, CanonicalViewDef> _views = new(StringComparer.OrdinalIgnoreCase);
    private GlobalState? _cachedGlobalState;

    /// <summary>
    /// All registered canonical views (main.* public hunting views).
    /// </summary>
    public IReadOnlyDictionary<string, CanonicalViewDef> Views => _views;

    /// <summary>
    /// Register a canonical view definition. Invalidates cached GlobalState.
    /// </summary>
    public void Register(CanonicalViewDef view)
    {
        _views[view.Name] = view;
        _cachedGlobalState = null;
    }

    /// <summary>
    /// Returns true if the given table name is an approved user-queryable view.
    /// </summary>
    public bool IsApproved(string tableName)
        => _views.ContainsKey(tableName);

    /// <summary>
    /// Resolves a table name to its canonical view definition, or null if not approved.
    /// </summary>
    public CanonicalViewDef? Resolve(string tableName)
        => _views.TryGetValue(tableName, out var view) ? view : null;

    /// <summary>
    /// Builds the Kusto.Language GlobalState with all approved views
    /// registered as TableSymbol instances in a synthetic database.
    /// </summary>
    public GlobalState BuildGlobalState()
    {
        if (_cachedGlobalState is not null)
        {
            return _cachedGlobalState;
        }

        var tables = _views.Values.Select(ToTableSymbol).ToArray();

        var db = new DatabaseSymbol("hunting", tables);
        _cachedGlobalState = GlobalState.Default.WithDatabase(db);

        return _cachedGlobalState;
    }

    /// <summary>
    /// Converts a canonical view definition to a Kusto TableSymbol
    /// using the inline schema string format: "(col1: type1, col2: type2, ...)"
    /// </summary>
    private static TableSymbol ToTableSymbol(CanonicalViewDef view)
    {
        var schemaStr = "(" + string.Join(", ",
            view.Columns.Select(c => $"{c.Name}: {c.KustoType.ToKustoName()}")) + ")";

        return new TableSymbol(view.Name, schemaStr);
    }
}
