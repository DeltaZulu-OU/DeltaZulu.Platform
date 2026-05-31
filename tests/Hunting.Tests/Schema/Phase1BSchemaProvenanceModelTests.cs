namespace Hunting.Tests.Schema;

using Hunting.Core.DuckDbSql;
using Hunting.Data;
using Hunting.Schema;
using Hunting.Schema.Definitions.Phase1B;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class Phase1BSchemaProvenanceModelTests
{
    private static readonly string[] ExpectedColumns =
    [
        "object_name",
        "object_kind",
        "schema_hash",
        "catalog_version",
        "applied_at"
    ];

    [TestMethod]
    public void Phase1B_InternalSchemaCatalog_DefinesExpectedColumnTypes()
    {
        var table = Phase1BInternalSchemaCatalog.SchemaProvenance;
        var columns = table.Columns.ToDictionary(static column => column.Name, StringComparer.OrdinalIgnoreCase);

        Assert.AreEqual(Hunting.Core.Schema.DuckDbType.Varchar, columns["object_name"].DuckDbType);
        Assert.AreEqual(Hunting.Core.Schema.DuckDbType.Varchar, columns["object_kind"].DuckDbType);
        Assert.AreEqual(Hunting.Core.Schema.DuckDbType.Varchar, columns["schema_hash"].DuckDbType);
        Assert.AreEqual(Hunting.Core.Schema.DuckDbType.Varchar, columns["catalog_version"].DuckDbType);
        Assert.AreEqual(Hunting.Core.Schema.DuckDbType.Timestamp, columns["applied_at"].DuckDbType);

        Assert.IsFalse(columns["object_name"].Nullable);
        Assert.IsFalse(columns["object_kind"].Nullable);
        Assert.IsFalse(columns["schema_hash"].Nullable);
        Assert.IsTrue(columns["catalog_version"].Nullable);
        Assert.IsFalse(columns["applied_at"].Nullable);
    }

    [TestMethod]
    public void Phase1B_InternalSchemaConventions_ExposeSchemaProvenanceTable()
    {
#pragma warning disable MSTEST0032 // Assertion condition is always true
        Assert.AreEqual("internal", SchemaConventions.InternalSchema);
#pragma warning restore MSTEST0032 // Assertion condition is always true

        var table = SchemaConventions.InternalTables.Single(static table =>
            table.QualifiedName == "internal.schema_provenance");

        CollectionAssert.AreEqual(ExpectedColumns, table.Columns.Select(static column => column.Name).ToArray());
        Assert.DoesNotContain(view => view.QualifiedName == table.QualifiedName, SchemaConventions.CanonicalViews);
    }

    [TestMethod]
    public void Phase1B_SchemaApplier_CanApplyInternalProvenanceTable()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = new SchemaApplier(factory);

        var ddl = new SchemaEmitter().EmitAll(
            rawTables: [],
            internalTables: SchemaConventions.InternalTables,
            parserViews: [],
            canonicalViews: []);

        applier.ApplyStatements(ddl);

        var count = applier.QueryScalar(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'internal' AND table_name = 'schema_provenance'");

        Assert.AreEqual(1, count);

        var mismatches = applier.Validate(Phase1BInternalSchemaCatalog.SchemaProvenance);
        Assert.IsEmpty(mismatches);
    }

    [TestMethod]
    public void Phase1B_SchemaEmitter_DoesNotEmitInternalSchemaWhenNoInternalTablesAreRequested()
    {
        var ddl = new SchemaEmitter().EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: [],
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        Assert.DoesNotContain(static sql => sql.Contains("CREATE SCHEMA IF NOT EXISTS internal", StringComparison.OrdinalIgnoreCase), ddl);
        Assert.DoesNotContain(static sql => sql.Contains("internal.schema_provenance", StringComparison.OrdinalIgnoreCase), ddl);
    }

    [TestMethod]
    public void Phase1B_SchemaEmitter_EmitsInternalSchemaAndProvenanceTableWhenRequested()
    {
        var ddl = new SchemaEmitter().EmitAll(
            rawTables: [],
            internalTables: SchemaConventions.InternalTables,
            parserViews: [],
            canonicalViews: []);

        Assert.Contains(static sql => sql.Contains("CREATE SCHEMA IF NOT EXISTS internal", StringComparison.OrdinalIgnoreCase), ddl);

        var tableDdl = ddl.Single(sql => sql.Contains("CREATE TABLE IF NOT EXISTS internal.schema_provenance", StringComparison.OrdinalIgnoreCase));

        foreach (var column in ExpectedColumns)
        {
            Assert.Contains(column, tableDdl);
        }

        Assert.Contains("object_name VARCHAR", tableDdl);
        Assert.Contains("object_kind VARCHAR", tableDdl);
        Assert.Contains("schema_hash VARCHAR", tableDdl);
        Assert.Contains("catalog_version VARCHAR", tableDdl);
        Assert.Contains("applied_at TIMESTAMP", tableDdl);
    }
}