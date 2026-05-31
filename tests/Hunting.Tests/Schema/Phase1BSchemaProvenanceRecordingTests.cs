namespace Hunting.Tests.Schema;

using Hunting.Core.DuckDbSql;
using Hunting.Core.Schema;
using Hunting.Data;
using Hunting.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class Phase1BSchemaProvenanceRecordingTests
{
    [TestMethod]
    public void SchemaProvenanceRecorder_RecordsOneRowPerAppliedSchemaObject()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);
        var recorder = new SchemaProvenanceRecorder(factory);

        var fingerprints = recorder.RecordAppliedSchemaProvenance(
            SchemaConventions.RawTables,
            SchemaConventions.InternalTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews);

        var expectedCount =
            SchemaConventions.RawTables.Count +
            SchemaConventions.InternalTables.Count +
            SchemaConventions.ParserViews.Count +
            SchemaConventions.CanonicalViews.Count;

        Assert.HasCount(expectedCount, fingerprints);
        Assert.AreEqual(expectedCount, applier.QueryScalar("SELECT count(*) FROM internal.schema_provenance"));
    }

    [TestMethod]
    public void SchemaProvenanceRecorder_RecordsExpectedObjectKinds()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        CreateAndApplySchema(factory);

        var recorder = new SchemaProvenanceRecorder(factory);
        recorder.RecordAppliedSchemaProvenance(
            SchemaConventions.RawTables,
            SchemaConventions.InternalTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews);

        var rows = recorder.ReadRecordedProvenance();
        var byName = rows.ToDictionary(static row => row.ObjectName, StringComparer.OrdinalIgnoreCase);

        Assert.AreEqual(SchemaFingerprint.RawTableKind, byName["bronze.windows_sysmon_event"].ObjectKind);
        Assert.AreEqual(SchemaFingerprint.RawTableKind, byName["bronze.windows_security_event"].ObjectKind);
        Assert.AreEqual(SchemaFingerprint.RawTableKind, byName["bronze.dns_server_event"].ObjectKind);
        Assert.AreEqual(SchemaFingerprint.InternalTableKind, byName["internal.schema_provenance"].ObjectKind);
        Assert.AreEqual(SchemaFingerprint.ParserViewKind, byName["silver.v_processevent_windows_sysmon_eid1"].ObjectKind);
        Assert.AreEqual(SchemaFingerprint.CanonicalViewKind, byName["golden.ProcessEvent"].ObjectKind);
    }

    [TestMethod]
    public void SchemaProvenanceRecorder_StoresStableHashesAndCatalogVersion()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        CreateAndApplySchema(factory);

        var recorder = new SchemaProvenanceRecorder(factory);
        recorder.RecordAppliedSchemaProvenance(
            SchemaConventions.RawTables,
            SchemaConventions.InternalTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews,
            catalogVersion: "test-version");

        var rows = recorder.ReadRecordedProvenance();

        Assert.IsNotEmpty(rows);
        Assert.IsTrue(rows.All(static row => row.SchemaHash.Length == 64));
        Assert.IsTrue(rows.All(static row => row.CatalogVersion == "test-version"));
    }

    [TestMethod]
    public void SchemaProvenanceRecorder_ReapplyIsIdempotentByObjectName()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndApplySchema(factory);
        var recorder = new SchemaProvenanceRecorder(factory);

        recorder.RecordAppliedSchemaProvenance(
            SchemaConventions.RawTables,
            SchemaConventions.InternalTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews,
            catalogVersion: "first");

        var firstCount = applier.QueryScalar("SELECT count(*) FROM internal.schema_provenance");

        recorder.RecordAppliedSchemaProvenance(
            SchemaConventions.RawTables,
            SchemaConventions.InternalTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews,
            catalogVersion: "second");

        var secondCount = applier.QueryScalar("SELECT count(*) FROM internal.schema_provenance");
        var rows = recorder.ReadRecordedProvenance();

        Assert.AreEqual(firstCount, secondCount);
        Assert.IsTrue(rows.All(static row => row.CatalogVersion == "second"));
    }

    [TestMethod]
    public void SchemaProvenanceRecorder_DoesNotClassifyOrBlockDriftYet()
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
            CREATE OR REPLACE VIEW golden.ProcessEvent AS
            SELECT * FROM golden.ProcessEvent
            """);

        recorder.RecordAppliedSchemaProvenance(
            SchemaConventions.RawTables,
            SchemaConventions.InternalTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews);

        Assert.IsGreaterThan(0, applier.QueryScalar("SELECT count(*) FROM internal.schema_provenance"));
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