using DeltaZulu.Platform.Domain.Analytics.Schema;
using DeltaZulu.Platform.Domain.Analytics.Schema.Catalog;

namespace DeltaZulu.Platform.Tests.Analytics.Schema.Catalog;

[TestClass]
public class SourceFieldCatalogTests
{
    [TestMethod]
    public void Constructor_RejectsIntKustoType()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new SourceFieldCatalogEntry("count", KustoType.Int, "cef:extension:cnt"));
    }

    [TestMethod]
    public void Constructor_RejectsNativeDecimalWithoutDecimalAnnotation()
    {
        // Without this, the Avro/Arrow projections would silently fall back to a lossy double.
        Assert.ThrowsExactly<ArgumentException>(() =>
            new SourceFieldCatalogEntry("amount", KustoType.Decimal, "cef:extension:cn2"));

        // A Decimal annotation makes it valid.
        _ = new SourceFieldCatalogEntry(
            "amount", KustoType.Decimal, "cef:extension:cn2",
            annotation: FieldAnnotation.Decimal(precision: 18, scale: 2));
    }

    [TestMethod]
    public void Constructor_RejectsDuplicateFieldNames()
    {
        var entries = new[] {
            new SourceFieldCatalogEntry("src", KustoType.String, "cef:extension:src"),
            new SourceFieldCatalogEntry("SRC", KustoType.String, "cef:extension:src2"),
        };

        Assert.ThrowsExactly<ArgumentException>(() => new SourceFieldCatalog("dup_source", entries));
    }

    [TestMethod]
    public void Constructor_RejectsEmptyCatalog()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new SourceFieldCatalog("empty_source", []));
    }

    [TestMethod]
    public void Constructor_RejectsPromotedNestedPathAnnotation()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new SourceFieldCatalogEntry(
                "agentBuild",
                KustoType.String,
                "cef:extension:cs3",
                annotation: FieldAnnotation.NestedPath("$.cs3"),
                promoted: true));
    }

    [TestMethod]
    public void FieldAnnotation_SimpleRejectsParameterizedKinds()
    {
        Assert.ThrowsExactly<ArgumentException>(() => FieldAnnotation.Simple(FieldAnnotationKind.Duration));
        Assert.ThrowsExactly<ArgumentException>(() => FieldAnnotation.Simple(FieldAnnotationKind.Bool));
        Assert.ThrowsExactly<ArgumentException>(() => FieldAnnotation.Simple(FieldAnnotationKind.Decimal));
        Assert.ThrowsExactly<ArgumentException>(() => FieldAnnotation.Simple(FieldAnnotationKind.NestedPath));
    }

    [TestMethod]
    public void RoundTrip_ToJsonThenLoadFromJson_PreservesAllFields()
    {
        var original = SourceFieldCatalogLibrary.CefFirewall;
        var reloaded = SourceFieldCatalog.LoadFromJson(original.ToJson());

        Assert.AreEqual(original.Source, reloaded.Source);
        Assert.AreEqual(original.Entries.Count, reloaded.Entries.Count);

        for (var i = 0; i < original.Entries.Count; i++)
        {
            var expected = original.Entries[i];
            var actual = reloaded.Entries.Single(e => e.Name == expected.Name);

            Assert.AreEqual(expected.KustoType, actual.KustoType);
            Assert.AreEqual(expected.ParserGrammarRef, actual.ParserGrammarRef);
            Assert.AreEqual(expected.Canonicalization, actual.Canonicalization);
            Assert.AreEqual(expected.Promoted, actual.Promoted);
            Assert.AreEqual(expected.Annotation?.Kind, actual.Annotation?.Kind);
        }
    }

    [TestMethod]
    public void CefFirewallCatalog_LoadsAndExercisesEveryAnnotationKindButIpv6()
    {
        var catalog = SourceFieldCatalogLibrary.CefFirewall;
        var kinds = catalog.Entries.Where(e => e.Annotation is not null).Select(e => e.Annotation!.Kind).ToHashSet();

        CollectionAssert.Contains(kinds.ToList(), FieldAnnotationKind.Ipv4);
        CollectionAssert.Contains(kinds.ToList(), FieldAnnotationKind.Mac48);
        CollectionAssert.Contains(kinds.ToList(), FieldAnnotationKind.Guid);
        CollectionAssert.Contains(kinds.ToList(), FieldAnnotationKind.Bool);
        CollectionAssert.Contains(kinds.ToList(), FieldAnnotationKind.Duration);
        CollectionAssert.Contains(kinds.ToList(), FieldAnnotationKind.Decimal);
        CollectionAssert.Contains(kinds.ToList(), FieldAnnotationKind.NestedPath);

        Assert.IsTrue(catalog.Entries.Any(e => e.Promoted), "catalog must exercise promotion");
        Assert.IsTrue(catalog.Entries.Any(e => e.KustoType == KustoType.Dynamic), "catalog must retain a bag column");
    }
}
