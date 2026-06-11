using DeltaZulu.Platform.Domain.Hunting.Schema;

namespace DeltaZulu.Platform.Tests.Hunting.Schema;

[TestClass]
public class EditorSchemaMetadataTests
{
    [TestMethod]
    public void FromCanonicalViews_ProjectsRichEditorMetadataFromGoldenContracts()
    {
        var metadata = SchemaConventions.EditorMetadata;
        var processEvent = metadata.Tables.Single(table => table.Name == "ProcessEvent");
        var additionalFields = processEvent.Columns.Single(column => column.Name == "AdditionalFields");

        Assert.AreEqual("Process execution and related process events across configured sources.", processEvent.Description);
        CollectionAssert.Contains(processEvent.Contributors.ToArray(), "silver.v_processevent_windows_sysmon_eid1");
        Assert.AreEqual("dynamic", additionalFields.Type);
        Assert.IsTrue(additionalFields.Nullable);
        Assert.IsTrue(additionalFields.Dynamic);
        Assert.AreEqual("Source-specific additional data.", additionalFields.Description);
    }

    [TestMethod]
    public void BuildEditorSchemaMetadata_MatchesApprovedPublicViewsOnly()
    {
        var catalog = new ApprovedViewCatalog();
        catalog.RegisterAll(SchemaConventions.CanonicalViews);

        var metadata = catalog.BuildEditorSchemaMetadata();

        CollectionAssert.AreEquivalent(
            SchemaConventions.CanonicalViews.Select(view => view.Name).ToArray(),
            metadata.Tables.Select(table => table.Name).ToArray());
        Assert.DoesNotContain(table => table.Name.StartsWith("v_", StringComparison.Ordinal), metadata.Tables);
        Assert.DoesNotContain(table => table.Name.Contains("windows_", StringComparison.Ordinal), metadata.Tables);
    }

    [TestMethod]
    public void FromCanonicalViews_LanguageMetadataIncludesSupportedTermsOnly()
    {
        var language = SchemaConventions.EditorMetadata.Language;

        CollectionAssert.Contains(language.Keywords.ToArray(), "sample-distinct");
        CollectionAssert.Contains(language.Operators.ToArray(), "contains_cs");
        CollectionAssert.Contains(language.Operators.ToArray(), "!contains_cs");
        CollectionAssert.DoesNotContain(language.RenderKinds.ToArray(), "card");
        CollectionAssert.DoesNotContain(language.Keywords.ToArray(), "mv-expand");
        CollectionAssert.DoesNotContain(language.Keywords.ToArray(), "fork");
        CollectionAssert.DoesNotContain(language.Keywords.ToArray(), "union");
    }

    [TestMethod]
    public void FromCanonicalViews_ColumnsExactlyMatchGoldenContractOrder()
    {
        var metadata = EditorSchemaMetadata.FromCanonicalViews(SchemaConventions.CanonicalViews);

        foreach (var view in SchemaConventions.CanonicalViews)
        {
            var table = metadata.Tables.Single(candidate => candidate.Name == view.Name);
            CollectionAssert.AreEqual(
                view.Columns.Select(column => column.Name).ToArray(),
                table.Columns.Select(column => column.Name).ToArray(),
                $"Editor columns for {view.Name} must follow its Golden contract exactly.");
        }
    }
}