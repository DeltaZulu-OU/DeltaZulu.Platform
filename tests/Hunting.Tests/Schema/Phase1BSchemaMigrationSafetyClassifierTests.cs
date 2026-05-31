namespace Hunting.Tests.Schema;

using Hunting.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class Phase1BSchemaMigrationSafetyClassifierTests
{
    [TestMethod]
    public void Classifier_CanSummarizeSafeAndUnsafeDrift()
    {
        var classifier = new SchemaMigrationSafetyClassifier();

        var results = classifier.Classify(
        [
            new SchemaProvenanceDrift(
                "golden.ProcessEvent",
                "canonical_view",
                SchemaProvenanceDriftStatus.Unchanged,
                ExpectedHash: "a",
                RecordedHash: "a",
                Message: "unchanged"),
            new SchemaProvenanceDrift(
                "golden.FileEvent",
                "canonical_view",
                SchemaProvenanceDriftStatus.NewObject,
                ExpectedHash: "b",
                RecordedHash: null,
                Message: "new"),
            new SchemaProvenanceDrift(
                "golden.ObsoleteEvent",
                "canonical_view",
                SchemaProvenanceDriftStatus.MissingObject,
                ExpectedHash: null,
                RecordedHash: "c",
                Message: "missing"),
            new SchemaProvenanceDrift(
                "golden.Dns",
                "canonical_view",
                SchemaProvenanceDriftStatus.ChangedObject,
                ExpectedHash: "d",
                RecordedHash: "e",
                Message: "changed")
        ]);

        Assert.AreEqual(2, results.Count(static result => result.Safety == SchemaMigrationSafety.Safe));
        Assert.AreEqual(2, results.Count(static result => result.Safety == SchemaMigrationSafety.Unsafe));
        Assert.AreEqual(2, results.Count(static result => result.RequiresExplicitApproval));
    }

    [TestMethod]
    public void Classifier_ProducesDeterministicOrdering()
    {
        var classifier = new SchemaMigrationSafetyClassifier();

        var results = classifier.Classify(
        [
            new SchemaProvenanceDrift(
                "golden.NetworkSession",
                "canonical_view",
                SchemaProvenanceDriftStatus.Unchanged,
                ExpectedHash: "a",
                RecordedHash: "a",
                Message: "unchanged"),
            new SchemaProvenanceDrift(
                "bronze.windows_sysmon_event",
                "raw_table",
                SchemaProvenanceDriftStatus.NewObject,
                ExpectedHash: "b",
                RecordedHash: null,
                Message: "new"),
            new SchemaProvenanceDrift(
                "golden.Dns",
                "canonical_view",
                SchemaProvenanceDriftStatus.ChangedObject,
                ExpectedHash: "c",
                RecordedHash: "d",
                Message: "changed")
        ]);

        CollectionAssert.AreEqual(
            new[]
            {
                "bronze.windows_sysmon_event",
                "golden.Dns",
                "golden.NetworkSession"
            },
            results.Select(static result => result.ObjectName).ToArray());
    }

    [TestMethod]
    public void Classifier_TreatsChangedObjectsAsUnsafeByDefault()
    {
        var classifier = new SchemaMigrationSafetyClassifier();

        var result = classifier.Classify(
        [
            new SchemaProvenanceDrift(
                "golden.ProcessEvent",
                "canonical_view",
                SchemaProvenanceDriftStatus.ChangedObject,
                ExpectedHash: "new",
                RecordedHash: "old",
                Message: "changed")
        ]).Single();

        Assert.AreEqual(SchemaMigrationSafety.Unsafe, result.Safety);
        Assert.IsTrue(result.RequiresExplicitApproval);
        Assert.Contains("cannot yet prove", result.Message);
    }

    [TestMethod]
    public void Classifier_TreatsMissingObjectsAsUnsafeByDefault()
    {
        var classifier = new SchemaMigrationSafetyClassifier();

        var result = classifier.Classify(
        [
            new SchemaProvenanceDrift(
                "golden.ObsoleteEvent",
                "canonical_view",
                SchemaProvenanceDriftStatus.MissingObject,
                ExpectedHash: null,
                RecordedHash: "old",
                Message: "missing")
        ]).Single();

        Assert.AreEqual(SchemaMigrationSafety.Unsafe, result.Safety);
        Assert.IsTrue(result.RequiresExplicitApproval);
        Assert.Contains("Removal is unsafe", result.Message);
    }

    [TestMethod]
    public void Classifier_TreatsNewObjectsAsSafe()
    {
        var classifier = new SchemaMigrationSafetyClassifier();

        var result = classifier.Classify(
        [
            new SchemaProvenanceDrift(
                "golden.FileEvent",
                "canonical_view",
                SchemaProvenanceDriftStatus.NewObject,
                ExpectedHash: "a",
                RecordedHash: null,
                Message: "new")
        ]).Single();

        Assert.AreEqual(SchemaMigrationSafety.Safe, result.Safety);
        Assert.IsFalse(result.RequiresExplicitApproval);
    }

    [TestMethod]
    public void Classifier_TreatsUnchangedObjectsAsSafe()
    {
        var classifier = new SchemaMigrationSafetyClassifier();

        var result = classifier.Classify(
        [
            new SchemaProvenanceDrift(
                "golden.ProcessEvent",
                "canonical_view",
                SchemaProvenanceDriftStatus.Unchanged,
                ExpectedHash: "a",
                RecordedHash: "a",
                Message: "unchanged")
        ]).Single();

        Assert.AreEqual(SchemaMigrationSafety.Safe, result.Safety);
        Assert.IsFalse(result.RequiresExplicitApproval);
    }
}