using DeltaZulu.Platform.Domain.Analytics.Catalog;
using DeltaZulu.Platform.Domain.Analytics.Schema;

namespace DeltaZulu.Platform.Tests.Analytics.Translation;

[TestClass]
public sealed class ApprovedViewCatalogSchemaAdapterTests
{
    private static ApprovedViewCatalog CreateCatalog()
    {
        var catalog = new ApprovedViewCatalog();
        catalog.RegisterAll(SchemaConventions.CanonicalViews);
        return catalog;
    }

    [TestMethod]
    public void TryGetTable_ResolvesRegisteredView()
    {
        var adapter = new ApprovedViewCatalogSchemaAdapter(CreateCatalog());

        var found = adapter.TryGetTable("ProcessEvent", out var schema);

        Assert.IsTrue(found);
        Assert.AreEqual("ProcessEvent", schema.Name);
        Assert.IsTrue(schema.Columns.Count > 0);
    }

    [TestMethod]
    public void TryGetTable_ResolvesCaseInsensitively()
    {
        var adapter = new ApprovedViewCatalogSchemaAdapter(CreateCatalog());

        Assert.IsTrue(adapter.TryGetTable("processevent", out _));
    }

    [TestMethod]
    public void TryGetTable_ReturnsFalseForUnapprovedTable()
    {
        var adapter = new ApprovedViewCatalogSchemaAdapter(CreateCatalog());

        Assert.IsFalse(adapter.TryGetTable("silver.secret_table", out _));
    }

    [TestMethod]
    public void Tables_EnumeratesEveryRegisteredView()
    {
        var catalog = CreateCatalog();
        var adapter = new ApprovedViewCatalogSchemaAdapter(catalog);

        var adapterNames = adapter.Tables.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var catalogNames = catalog.Views.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        CollectionAssert.AreEquivalent(catalogNames.ToArray(), adapterNames.ToArray());
    }

    [TestMethod]
    public void Version_MatchesCatalogVersion()
    {
        var catalog = CreateCatalog();
        var adapter = new ApprovedViewCatalogSchemaAdapter(catalog);

        Assert.AreEqual(catalog.CatalogVersion, adapter.Version);
    }

    [TestMethod]
    public void BuildGlobalState_DelegatesToCatalogsCachedInstance()
    {
        var catalog = CreateCatalog();
        var adapter = new ApprovedViewCatalogSchemaAdapter(catalog);

        var viaAdapter = adapter.BuildGlobalState();
        var viaCatalog = catalog.BuildGlobalState();

        Assert.AreSame(viaCatalog, viaAdapter,
            "The adapter must reuse ApprovedViewCatalog's own cached GlobalState, not build a second one.");
    }

    [TestMethod]
    public void ColumnTypes_CarryTheDeclaredKustoTypeName()
    {
        var adapter = new ApprovedViewCatalogSchemaAdapter(CreateCatalog());

        adapter.TryGetTable("ProcessEvent", out var schema);
        var processId = schema.Columns.Single(c => c.Name == "ProcessId");
        var fileName = schema.Columns.Single(c => c.Name == "FileName");

        Assert.AreEqual("long", processId.TypeName);
        Assert.AreEqual("string", fileName.TypeName);
    }
}
