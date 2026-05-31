namespace Hunting.Tests.Schema;

using Hunting.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class Phase1BSchemaMigrationSafetyGuardTests
{
    [TestMethod]
    public void Guard_AllowsSafeAssessmentsByDefault()
    {
        var guard = new SchemaMigrationSafetyGuard();

        var report = guard.Evaluate(
        [
            Safe("bronze.windows_sysmon_event", SchemaProvenanceDriftStatus.Unchanged),
            Safe("golden.FileEvent", SchemaProvenanceDriftStatus.NewObject)
        ]);

        Assert.IsFalse(report.ShouldBlock);
        Assert.AreEqual(SchemaMigrationSafetyPolicy.BlockUnsafe, report.Policy);
        Assert.IsEmpty(report.UnsafeAssessments);
        Assert.Contains("passed", report.Message);
    }

    [TestMethod]
    public void Guard_BlocksUnsafeAssessmentsByDefault()
    {
        var guard = new SchemaMigrationSafetyGuard();

        var report = guard.Evaluate(
        [
            Safe("bronze.windows_sysmon_event", SchemaProvenanceDriftStatus.Unchanged),
            Unsafe("golden.ProcessEvent", SchemaProvenanceDriftStatus.ChangedObject)
        ]);

        Assert.IsTrue(report.ShouldBlock);
        Assert.HasCount(1, report.UnsafeAssessments);
        Assert.AreEqual("golden.ProcessEvent", report.UnsafeAssessments[0].ObjectName);
        Assert.Contains("blocked", report.Message);
    }

    [TestMethod]
    public void Guard_AllowUnsafePolicyReportsButDoesNotBlock()
    {
        var guard = new SchemaMigrationSafetyGuard();

        var report = guard.Evaluate(
        [
            Unsafe("golden.ProcessEvent", SchemaProvenanceDriftStatus.ChangedObject)
        ], SchemaMigrationSafetyPolicy.AllowUnsafe);

        Assert.IsFalse(report.ShouldBlock);
        Assert.HasCount(1, report.UnsafeAssessments);
        Assert.Contains("allows continuation", report.Message);
    }

    [TestMethod]
    public void Guard_ThrowIfBlocked_ThrowsForUnsafeDefaultPolicy()
    {
        var guard = new SchemaMigrationSafetyGuard();

        var ex = Assert.ThrowsExactly<SchemaMigrationSafetyException>(() =>
            guard.ThrowIfBlocked(
            [
                Unsafe("golden.ProcessEvent", SchemaProvenanceDriftStatus.ChangedObject)
            ]));

        Assert.IsTrue(ex.Report.ShouldBlock);
        Assert.AreEqual("golden.ProcessEvent", ex.Report.UnsafeAssessments.Single().ObjectName);
    }

    [TestMethod]
    public void Guard_ThrowIfBlocked_DoesNotThrowForAllowUnsafePolicy()
    {
        var guard = new SchemaMigrationSafetyGuard();

        guard.ThrowIfBlocked(
        [
            Unsafe("golden.ProcessEvent", SchemaProvenanceDriftStatus.ChangedObject)
        ], SchemaMigrationSafetyPolicy.AllowUnsafe);
    }

    [TestMethod]
    public void Guard_ProducesDeterministicAssessmentOrdering()
    {
        var guard = new SchemaMigrationSafetyGuard();

        var report = guard.Evaluate(
        [
            Safe("golden.NetworkSession", SchemaProvenanceDriftStatus.Unchanged),
            Unsafe("golden.ProcessEvent", SchemaProvenanceDriftStatus.ChangedObject),
            Safe("bronze.windows_sysmon_event", SchemaProvenanceDriftStatus.NewObject)
        ]);

        CollectionAssert.AreEqual(
            new[]
            {
                "bronze.windows_sysmon_event",
                "golden.NetworkSession",
                "golden.ProcessEvent"
            },
            report.Assessments.Select(static assessment => assessment.ObjectName).ToArray());
    }

    private static SchemaMigrationSafetyAssessment Safe(
        string objectName,
        SchemaProvenanceDriftStatus driftStatus) =>
        new(
            objectName,
            ObjectKind(objectName),
            driftStatus,
            SchemaMigrationSafety.Safe,
            RequiresExplicitApproval: false,
            "safe");

    private static SchemaMigrationSafetyAssessment Unsafe(
        string objectName,
        SchemaProvenanceDriftStatus driftStatus) =>
        new(
            objectName,
            ObjectKind(objectName),
            driftStatus,
            SchemaMigrationSafety.Unsafe,
            RequiresExplicitApproval: true,
            "unsafe");

    private static string ObjectKind(string objectName) =>
        objectName.StartsWith("golden.", StringComparison.OrdinalIgnoreCase)
            ? "canonical_view"
            : "raw_table";
}