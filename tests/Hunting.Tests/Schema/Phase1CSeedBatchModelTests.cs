namespace Hunting.Tests.Schema;

using Hunting.Core.DuckDbSql;
using Hunting.Core.Schema;
using Hunting.Data;
using Hunting.Schema;
using Hunting.Schema.Definitions.Phase1B;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class Phase1CSeedBatchModelTests
{
    private static readonly string[] ExpectedSeedBatchColumns =
    [
        "batch_id",
        "table_name",
        "source_name",
        "scenario",
        "row_count",
        "content_hash",
        "catalog_version",
        "applied_at"
    ];

    [TestMethod]
    public void Phase1C_InternalSchemaCatalog_ExposesSeedBatchesTable()
    {
        var table = Phase1BInternalSchemaCatalog.SeedBatches;

        Assert.AreEqual("internal.seed_batches", table.QualifiedName);
        CollectionAssert.AreEqual(
            ExpectedSeedBatchColumns,
            table.Columns.Select(static column => column.Name).ToArray());

        Assert.Contains(static table => table.QualifiedName == "internal.schema_provenance", SchemaConventions.InternalTables);
        Assert.Contains(static table => table.QualifiedName == "internal.seed_batches", SchemaConventions.InternalTables);
    }

    [TestMethod]
    public void Phase1C_SeedBatchesTable_DefinesExpectedColumnTypesAndNullability()
    {
        var columns = Phase1BInternalSchemaCatalog.SeedBatches.Columns
            .ToDictionary(static column => column.Name, StringComparer.OrdinalIgnoreCase);

        Assert.AreEqual(DuckDbType.Varchar, columns["batch_id"].DuckDbType);
        Assert.AreEqual(DuckDbType.Varchar, columns["table_name"].DuckDbType);
        Assert.AreEqual(DuckDbType.Varchar, columns["source_name"].DuckDbType);
        Assert.AreEqual(DuckDbType.Varchar, columns["scenario"].DuckDbType);
        Assert.AreEqual(DuckDbType.BigInt, columns["row_count"].DuckDbType);
        Assert.AreEqual(DuckDbType.Varchar, columns["content_hash"].DuckDbType);
        Assert.AreEqual(DuckDbType.Varchar, columns["catalog_version"].DuckDbType);
        Assert.AreEqual(DuckDbType.Timestamp, columns["applied_at"].DuckDbType);

        Assert.IsFalse(columns["batch_id"].Nullable);
        Assert.IsFalse(columns["table_name"].Nullable);
        Assert.IsFalse(columns["source_name"].Nullable);
        Assert.IsFalse(columns["scenario"].Nullable);
        Assert.IsFalse(columns["row_count"].Nullable);
        Assert.IsFalse(columns["content_hash"].Nullable);
        Assert.IsTrue(columns["catalog_version"].Nullable);
        Assert.IsFalse(columns["applied_at"].Nullable);
    }

    [TestMethod]
    public void Phase1C_SeedBatchesTable_IsInternalOnly()
    {
        Assert.DoesNotContain(static view => view.QualifiedName == "internal.seed_batches", SchemaConventions.CanonicalViews);
        Assert.DoesNotContain(static view => view.QualifiedName == "internal.seed_batches", SchemaConventions.ParserViews);
        Assert.DoesNotContain(static table => table.QualifiedName == "internal.seed_batches", SchemaConventions.RawTables);
    }

    [TestMethod]
    public void Phase1C_SchemaEmitter_EmitsSeedBatchesTableWhenInternalTablesAreRequested()
    {
        var ddl = new SchemaEmitter().EmitAll(
            rawTables: [],
            internalTables: SchemaConventions.InternalTables,
            parserViews: [],
            canonicalViews: []);

        Assert.Contains(static sql => sql.Contains("CREATE SCHEMA IF NOT EXISTS internal", StringComparison.OrdinalIgnoreCase), ddl);

        var tableDdl = ddl.Single(sql => sql.Contains("CREATE TABLE IF NOT EXISTS internal.seed_batches", StringComparison.OrdinalIgnoreCase));

        foreach (var column in ExpectedSeedBatchColumns)
        {
            Assert.Contains(column, tableDdl);
        }

        Assert.Contains("batch_id VARCHAR", tableDdl);
        Assert.Contains("row_count BIGINT", tableDdl);
        Assert.Contains("applied_at TIMESTAMP", tableDdl);
    }

    [TestMethod]
    public void Phase1C_SchemaApplier_CanApplySeedBatchesTable()
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
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'internal' AND table_name = 'seed_batches'");

        Assert.AreEqual(1, count);

        var mismatches = applier.Validate(Phase1BInternalSchemaCatalog.SeedBatches);
        Assert.IsEmpty(mismatches);
    }
}