namespace Hunting.Tests.Schema;

using Hunting.Core.DuckDbSql;
using Hunting.Data;
using Hunting.Schema;
using Hunting.Schema.Definitions.Medallion;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class MedallionSchemaPipelineTests
{
    [TestMethod]
    public void SchemaConventions_UsesActiveMedallionCatalog()
    {
        CollectionAssert.AreEqual(
            MedallionSchemaCatalog.RawTables.Select(t => t.QualifiedName).ToArray(),
            SchemaConventions.RawTables.Select(t => t.QualifiedName).ToArray());

        CollectionAssert.AreEqual(
            MedallionSchemaCatalog.ParserViews.Select(v => v.QualifiedName).ToArray(),
            SchemaConventions.ParserViews.Select(v => v.QualifiedName).ToArray());

        CollectionAssert.AreEqual(
            MedallionSchemaCatalog.CanonicalViews.Select(v => v.QualifiedName).ToArray(),
            SchemaConventions.CanonicalViews.Select(v => v.QualifiedName).ToArray());
    }

    [TestMethod]
    public void SchemaEmitter_CanEmitActiveMedallionCatalog()
    {
        var ddl = new SchemaEmitter().EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: [],
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        Assert.AreEqual(
            3 + SchemaConventions.RawTables.Count + SchemaConventions.ParserViews.Count + SchemaConventions.CanonicalViews.Count,
            ddl.Count);

        Assert.IsTrue(ddl.Any(sql => sql.Contains("CREATE TABLE IF NOT EXISTS bronze.windows_sysmon_event")));
        Assert.IsTrue(ddl.Any(sql => sql.Contains("CREATE OR REPLACE VIEW silver.v_processevent_windows_sysmon_eid1")));
        Assert.IsTrue(ddl.Any(sql => sql.Contains("CREATE OR REPLACE VIEW golden.ProcessEvent")));
    }

    [TestMethod]
    public void SchemaApplier_CanApplyAndValidateActiveMedallionCatalog()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = new SchemaApplier(factory);
        var ddl = new SchemaEmitter().EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: [],
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        applier.ApplyStatements(ddl);

        foreach (var table in SchemaConventions.RawTables)
        {
            var mismatches = applier.Validate(table);
            Assert.AreEqual(0, mismatches.Count, $"{table.QualifiedName}: {string.Join("; ", mismatches.Select(m => m.Message))}");
        }

        foreach (var parserView in SchemaConventions.ParserViews)
        {
            var mismatches = applier.Validate(parserView);
            Assert.AreEqual(0, mismatches.Count, $"{parserView.QualifiedName}: {string.Join("; ", mismatches.Select(m => m.Message))}");
        }

        foreach (var canonicalView in SchemaConventions.CanonicalViews)
        {
            var mismatches = applier.Validate(canonicalView);
            Assert.AreEqual(0, mismatches.Count, $"{canonicalView.QualifiedName}: {string.Join("; ", mismatches.Select(m => m.Message))}");
        }
    }

    [TestMethod]
    public void SchemaApplier_ActiveMedallionCatalog_DoesNotCreateLegacyDemoTable()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = new SchemaApplier(factory);
        var ddl = new SchemaEmitter().EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: [],
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        applier.ApplyStatements(ddl);

        var legacyCount = applier.QueryScalar(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'bronze' AND table_name = 'windows_event_json'");

        Assert.AreEqual(0, legacyCount);
    }
}
