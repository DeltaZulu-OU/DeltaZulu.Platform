namespace Hunting.Tests.Schema;

using Hunting.Core.DuckDbSql;
using Hunting.Data;
using Hunting.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class Phase1BSchemaProvenanceDriftDetectionTests
{
    [TestMethod]
    public void DriftDetector_DoesNotBlockOrMutateRecordedRows()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);

        var recorder = new SchemaProvenanceRecorder(factory);
        recorder.RecordAppliedSchemaProvenance(
            SchemaConventions.RawTables,
            SchemaConventions.InternalTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews);

        var before = applier.QueryScalar("SELECT count(*) FROM internal.schema_provenance");

        var detector = new SchemaProvenanceDriftDetector(recorder);
        _ = DetectActiveSchema(detector);

        var after = applier.QueryScalar("SELECT count(*) FROM internal.schema_provenance");

        Assert.AreEqual(before, after);
    }

    [TestMethod]
    public void DriftDetector_ReportsAllObjectsAsNew_WhenNoProvenanceHasBeenRecorded()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        CreateAndApplySchema(factory);

        var detector = CreateDetector(factory);
        var drift = DetectActiveSchema(detector);

        Assert.IsNotEmpty(drift);
        Assert.IsTrue(drift.All(static item => item.Status == SchemaProvenanceDriftStatus.NewObject));
    }

    [TestMethod]
    public void DriftDetector_ReportsAllObjectsAsUnchanged_AfterRecordingCurrentCatalog()
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
        var drift = DetectActiveSchema(detector);

        Assert.IsNotEmpty(drift);
        Assert.IsTrue(drift.All(static item => item.Status == SchemaProvenanceDriftStatus.Unchanged));
    }

    [TestMethod]
    public void DriftDetector_ReportsChangedObject_WhenCurrentExpectedHashDiffersFromRecordedHash()
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
            SET schema_hash = '0000000000000000000000000000000000000000000000000000000000000000'
            WHERE object_name = 'golden.ProcessEvent'
            """);

        var detector = new SchemaProvenanceDriftDetector(recorder);
        var drift = DetectActiveSchema(detector);
        var processEvent = drift.Single(static item => item.ObjectName == "golden.ProcessEvent");

        Assert.AreEqual(SchemaProvenanceDriftStatus.ChangedObject, processEvent.Status);
        Assert.AreEqual("0000000000000000000000000000000000000000000000000000000000000000", processEvent.RecordedHash);
        Assert.IsNotNull(processEvent.ExpectedHash);
    }

    [TestMethod]
    public void DriftDetector_ReportsMissingObject_WhenRecordedObjectIsNoLongerExpected()
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
            INSERT INTO internal.schema_provenance
                (object_name, object_kind, schema_hash, catalog_version, applied_at)
            VALUES
                ('golden.ObsoleteEvent', 'canonical_view',
                 '1111111111111111111111111111111111111111111111111111111111111111',
                 'test', current_timestamp)
            """);

        var detector = new SchemaProvenanceDriftDetector(recorder);
        var drift = DetectActiveSchema(detector);
        var obsolete = drift.Single(static item => item.ObjectName == "golden.ObsoleteEvent");

        Assert.AreEqual(SchemaProvenanceDriftStatus.MissingObject, obsolete.Status);
        Assert.IsNull(obsolete.ExpectedHash);
        Assert.AreEqual("1111111111111111111111111111111111111111111111111111111111111111", obsolete.RecordedHash);
    }

    [TestMethod]
    public void DriftDetector_ReportsNewObject_WhenExpectedObjectWasNotRecorded()
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
            DELETE FROM internal.schema_provenance
            WHERE object_name = 'golden.Dns'
            """);

        var detector = new SchemaProvenanceDriftDetector(recorder);
        var drift = DetectActiveSchema(detector);
        var dns = drift.Single(static item => item.ObjectName == "golden.Dns");

        Assert.AreEqual(SchemaProvenanceDriftStatus.NewObject, dns.Status);
        Assert.IsNotNull(dns.ExpectedHash);
        Assert.IsNull(dns.RecordedHash);
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

    private static SchemaProvenanceDriftDetector CreateDetector(DuckDbConnectionFactory factory) =>
        new(new SchemaProvenanceRecorder(factory));

    private static IReadOnlyList<SchemaProvenanceDrift> DetectActiveSchema(SchemaProvenanceDriftDetector detector) =>
                detector.DetectDrift(
            SchemaConventions.RawTables,
            SchemaConventions.InternalTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews);
}