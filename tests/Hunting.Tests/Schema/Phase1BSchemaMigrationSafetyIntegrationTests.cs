namespace Hunting.Tests.Schema;

using Hunting.Core.DuckDbSql;
using Hunting.Data;
using Hunting.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class Phase1BSchemaMigrationSafetyIntegrationTests
{
    [TestMethod]
    public void Classifier_ReportsCurrentCatalogAsSafeAfterRecording()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        CreateAndApplySchema(factory);

        var recorder = new SchemaProvenanceRecorder(factory);
        recorder.RecordAppliedSchemaProvenance(
            SchemaConventions.RawTables,
            SchemaConventions.InternalTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews);

        var detector = new SchemaProvenanceDriftDetector(recorder);
        var drift = detector.DetectDrift(
            SchemaConventions.RawTables,
            SchemaConventions.InternalTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews);

        var assessments = new SchemaMigrationSafetyClassifier().Classify(drift);

        Assert.IsNotEmpty(assessments);
        Assert.IsTrue(assessments.All(static assessment => assessment.Safety == SchemaMigrationSafety.Safe));
        Assert.IsTrue(assessments.All(static assessment => !assessment.RequiresExplicitApproval));
    }

    [TestMethod]
    public void Classifier_ReportsTamperedProvenanceAsUnsafe()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);

        var recorder = new SchemaProvenanceRecorder(factory);
        recorder.RecordAppliedSchemaProvenance(
            SchemaConventions.RawTables,
            SchemaConventions.InternalTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews);

        applier.ExecuteRaw(
            """
            UPDATE internal.schema_provenance
            SET schema_hash = 'ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff'
            WHERE object_name = 'golden.ProcessEvent'
            """);

        var detector = new SchemaProvenanceDriftDetector(recorder);
        var drift = detector.DetectDrift(
            SchemaConventions.RawTables,
            SchemaConventions.InternalTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews);

        var assessments = new SchemaMigrationSafetyClassifier().Classify(drift);
        var processEvent = assessments.Single(static assessment => assessment.ObjectName == "golden.ProcessEvent");

        Assert.AreEqual(SchemaMigrationSafety.Unsafe, processEvent.Safety);
        Assert.IsTrue(processEvent.RequiresExplicitApproval);
    }

    [TestMethod]
    public void Classifier_ReportsUnrecordedCatalogAsSafeNewObjects()
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

        Assert.IsNotEmpty(assessments);
        Assert.IsTrue(assessments.All(static assessment => assessment.DriftStatus == SchemaProvenanceDriftStatus.NewObject));
        Assert.IsTrue(assessments.All(static assessment => assessment.Safety == SchemaMigrationSafety.Safe));
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