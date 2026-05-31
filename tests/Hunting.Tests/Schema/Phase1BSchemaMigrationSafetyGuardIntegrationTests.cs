namespace Hunting.Tests.Schema;

using Hunting.Core.DuckDbSql;
using Hunting.Data;
using Hunting.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class Phase1BSchemaMigrationSafetyGuardIntegrationTests
{
    [TestMethod]
    public void Guard_AllowsCurrentRecordedCatalog()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        CreateAndApplySchema(factory);

        var assessments = BuildAssessments(factory);
        var report = new SchemaMigrationSafetyGuard().Evaluate(assessments);

        Assert.IsFalse(report.ShouldBlock);
        Assert.IsNotEmpty(report.Assessments);
        Assert.IsTrue(report.Assessments.All(static assessment => assessment.Safety == SchemaMigrationSafety.Safe));
    }

    [TestMethod]
    public void Guard_BlocksTamperedRecordedHashByDefault()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);

        RecordCurrent(factory);

        applier.ExecuteRaw(
            """
            UPDATE internal.schema_provenance
            SET schema_hash = 'eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee'
            WHERE object_name = 'golden.ProcessEvent'
            """);

        var assessments = DetectAndClassify(factory);
        var report = new SchemaMigrationSafetyGuard().Evaluate(assessments);

        Assert.IsTrue(report.ShouldBlock);
        Assert.Contains(static assessment => assessment.ObjectName == "golden.ProcessEvent", report.UnsafeAssessments);
    }

    [TestMethod]
    public void Guard_AllowUnsafePolicyDoesNotBlockTamperedRecordedHash()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);

        RecordCurrent(factory);

        applier.ExecuteRaw(
            """
            UPDATE internal.schema_provenance
            SET schema_hash = 'dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd'
            WHERE object_name = 'golden.ProcessEvent'
            """);

        var assessments = DetectAndClassify(factory);
        var report = new SchemaMigrationSafetyGuard().Evaluate(
            assessments,
            SchemaMigrationSafetyPolicy.AllowUnsafe);

        Assert.IsFalse(report.ShouldBlock);
        Assert.Contains(static assessment => assessment.ObjectName == "golden.ProcessEvent", report.UnsafeAssessments);
    }

    [TestMethod]
    public void Guard_AllowsUnrecordedCatalogAsInitialSafeNewObjects()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        CreateAndApplySchema(factory);

        var recorder = new SchemaProvenanceRecorder(factory);
        var detector = new SchemaProvenanceDriftDetector(recorder);
        var drift = detector.DetectDrift(
            SchemaConventions.RawTables,
            SchemaConventions.InternalTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews);

        var assessments = new SchemaMigrationSafetyClassifier().Classify(drift);
        var report = new SchemaMigrationSafetyGuard().Evaluate(assessments);

        Assert.IsFalse(report.ShouldBlock);
        Assert.IsTrue(report.Assessments.All(static assessment => assessment.DriftStatus == SchemaProvenanceDriftStatus.NewObject));
    }

    private static IReadOnlyList<SchemaMigrationSafetyAssessment> BuildAssessments(DuckDbConnectionFactory factory)
    {
        RecordCurrent(factory);
        return DetectAndClassify(factory);
    }

    private static void RecordCurrent(DuckDbConnectionFactory factory)
    {
        var recorder = new SchemaProvenanceRecorder(factory);
        recorder.RecordAppliedSchemaProvenance(
            SchemaConventions.RawTables,
            SchemaConventions.InternalTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews);
    }

    private static IReadOnlyList<SchemaMigrationSafetyAssessment> DetectAndClassify(DuckDbConnectionFactory factory)
    {
        var recorder = new SchemaProvenanceRecorder(factory);
        var detector = new SchemaProvenanceDriftDetector(recorder);
        var drift = detector.DetectDrift(
            SchemaConventions.RawTables,
            SchemaConventions.InternalTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews);

        return new SchemaMigrationSafetyClassifier().Classify(drift);
    }

    private static SchemaApplier CreateAndApplySchema(DuckDbConnectionFactory factory)
    {
        var applier = new SchemaApplier(factory);
        var ddl = new SchemaEmitter().EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: SchemaConventions.InternalTables,
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        applier.ApplyStatements(ddl);
        return applier;
    }
}