namespace Hunting.Tests.Schema;

using Hunting.Core.DuckDbSql;
using Hunting.Core.Schema;
using Hunting.Schema;
using Hunting.Schema.Definitions.Phase1B;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class Phase1BSchemaFingerprintTests
{
    [TestMethod]
    public void SchemaFingerprint_InternalTable_IsStableForSameDefinition()
    {
        var first = SchemaFingerprint.FromInternalTable(Phase1BInternalSchemaCatalog.SchemaProvenance);
        var second = SchemaFingerprint.FromInternalTable(Phase1BInternalSchemaCatalog.SchemaProvenance);

        Assert.AreEqual("internal.schema_provenance", first.ObjectName);
        Assert.AreEqual(SchemaFingerprint.InternalTableKind, first.ObjectKind);
        Assert.AreEqual(first.SchemaHash, second.SchemaHash);
        Assert.AreEqual(64, first.SchemaHash.Length);
    }

    [TestMethod]
    public void SchemaFingerprint_InternalTable_ChangesWhenColumnTypeChanges()
    {
        var original = SchemaFingerprint.FromInternalTable(Phase1BInternalSchemaCatalog.SchemaProvenance);

        var changed = Phase1BInternalSchemaCatalog.SchemaProvenance with
        {
            Columns = Phase1BInternalSchemaCatalog.SchemaProvenance.Columns
                .Select(static column => column.Name == "schema_hash"
                    ? column with { DuckDbType = DuckDbType.Json, KustoType = KustoType.Dynamic }
                    : column)
                .ToArray()
        };

        var changedFingerprint = SchemaFingerprint.FromInternalTable(changed);

        Assert.AreNotEqual(original.SchemaHash, changedFingerprint.SchemaHash);
    }

    [TestMethod]
    public void SchemaFingerprint_InternalTable_ChangesWhenColumnOrderChanges()
    {
        var original = SchemaFingerprint.FromInternalTable(Phase1BInternalSchemaCatalog.SchemaProvenance);

        var reordered = Phase1BInternalSchemaCatalog.SchemaProvenance with
        {
            Columns = Phase1BInternalSchemaCatalog.SchemaProvenance.Columns.Reverse().ToArray()
        };

        var reorderedFingerprint = SchemaFingerprint.FromInternalTable(reordered);

        Assert.AreNotEqual(original.SchemaHash, reorderedFingerprint.SchemaHash);
    }

    [TestMethod]
    public void SchemaFingerprint_RawTable_ChangesWhenSourceDescriptionChanges()
    {
        var table = SchemaConventions.RawTables.Single(static table => table.QualifiedName == "bronze.windows_sysmon_event");
        var original = SchemaFingerprint.FromRawTable(table);

        var changed = table with { SourceDescription = table.SourceDescription + " changed" };
        var changedFingerprint = SchemaFingerprint.FromRawTable(changed);

        Assert.AreNotEqual(original.SchemaHash, changedFingerprint.SchemaHash);
    }

    [TestMethod]
    public void SchemaFingerprint_ParserView_IsStableForSameEmittedSql()
    {
        var emitter = new SchemaEmitter();
        var view = SchemaConventions.ParserViews.Single(static view => view.QualifiedName == "silver.v_processevent_windows_sysmon_eid1");
        var sql = emitter.EmitParserView(view);

        var first = SchemaFingerprint.FromParserView(view, sql);
        var second = SchemaFingerprint.FromParserView(view, sql);

        Assert.AreEqual(view.QualifiedName, first.ObjectName);
        Assert.AreEqual(SchemaFingerprint.ParserViewKind, first.ObjectKind);
        Assert.AreEqual(first.SchemaHash, second.SchemaHash);
    }

    [TestMethod]
    public void SchemaFingerprint_ParserView_NormalizesWhitespaceInEmittedSql()
    {
        var emitter = new SchemaEmitter();
        var view = SchemaConventions.ParserViews.Single(static view => view.QualifiedName == "silver.v_processevent_windows_sysmon_eid1");
        var sql = emitter.EmitParserView(view);
        var paddedSql = sql.Replace("\n", "\r\n    ");

        var first = SchemaFingerprint.FromParserView(view, sql);
        var second = SchemaFingerprint.FromParserView(view, paddedSql);

        Assert.AreEqual(first.SchemaHash, second.SchemaHash);
    }

    [TestMethod]
    public void SchemaFingerprint_ParserView_ChangesWhenEmittedSqlChanges()
    {
        var emitter = new SchemaEmitter();
        var view = SchemaConventions.ParserViews.Single(static view => view.QualifiedName == "silver.v_processevent_windows_sysmon_eid1");
        var sql = emitter.EmitParserView(view);

        var first = SchemaFingerprint.FromParserView(view, sql);
        var second = SchemaFingerprint.FromParserView(view, sql + "\nWHERE 1 = 1");

        Assert.AreNotEqual(first.SchemaHash, second.SchemaHash);
    }

    [TestMethod]
    public void SchemaFingerprint_CanonicalView_ChangesWhenParserBranchOrderChanges()
    {
        var emitter = new SchemaEmitter();
        var view = SchemaConventions.CanonicalViews.Single(static view => view.QualifiedName == "golden.ProcessEvent");
        var sql = emitter.EmitCanonicalView(view);

        var original = SchemaFingerprint.FromCanonicalView(view, sql);

        var changed = view with
        {
            ParserViews = view.ParserViews.Reverse().ToArray()
        };

        var changedSql = emitter.EmitCanonicalView(changed);
        var changedFingerprint = SchemaFingerprint.FromCanonicalView(changed, changedSql);

        Assert.AreNotEqual(original.SchemaHash, changedFingerprint.SchemaHash);
    }

    [TestMethod]
    public void SchemaFingerprint_NormalizeSql_CollapsesFormattingNoise()
    {
        var first = SchemaFingerprint.NormalizeSql(
            """
            CREATE VIEW x AS
            SELECT
                a,
                b
            FROM y
            """);

        var second = SchemaFingerprint.NormalizeSql("CREATE VIEW x AS SELECT a, b FROM y");

        Assert.AreEqual(second, first);
    }
}