using System.Text.Json;
using DeltaZulu.Platform.Data.DuckDb.Sql;
using DeltaZulu.Platform.Data.Proton;
using DeltaZulu.Platform.Domain.Analytics.Schema;
using DeltaZulu.Platform.Domain.Analytics.Schema.Catalog;
using DeltaZulu.Platform.Domain.Analytics.Schema.Catalog.Projections;

namespace DeltaZulu.Platform.Tests.Analytics.Schema.Catalog;

/// <summary>
/// Phase 2 exit criterion: one real (CEF-heavy) source fully described in the catalog,
/// all five projections generating.
/// </summary>
[TestClass]
public class CatalogProjectionTests
{
    private static SourceFieldCatalog Cef => SourceFieldCatalogLibrary.CefFirewall;

    [TestMethod]
    public void ColumnProjection_ProducesOneColumnPerField_PromotedFieldIsTopLevel()
    {
        var columns = CatalogColumnProjection.ToColumns(Cef);

        Assert.AreEqual(Cef.Entries.Count, columns.Count);
        var ruleName = columns.Single(c => c.Name == "ruleName");
        Assert.AreEqual(DuckDbType.Varchar, ruleName.DuckDbType);

        // The Decimal annotation keeps DuckDB's existing (lossy) Decimal->Double default;
        // fidelity is preserved in the Avro/Arrow projections instead (ADR-2).
        var amount = columns.Single(c => c.Name == "transactionAmount");
        Assert.AreEqual(DuckDbType.Double, amount.DuckDbType);
    }

    [TestMethod]
    public void DuckDbDdl_CreatesSilverTableWithAllColumns()
    {
        var ddl = CatalogDdlGenerator.EmitCreateTable(Cef);

        StringAssert.Contains(ddl, "CREATE TABLE IF NOT EXISTS silver.cef_firewall");
        StringAssert.Contains(ddl, "ruleName VARCHAR");
        StringAssert.Contains(ddl, "extensionsRaw JSON");
        StringAssert.Contains(ddl, "sessionDurationMs BIGINT");
    }

    [TestMethod]
    public void ProtonDdl_CreatesStreamWithAllColumns()
    {
        var ddl = ProtonCatalogDdlGenerator.EmitStream(Cef);

        StringAssert.Contains(ddl, "CREATE STREAM IF NOT EXISTS silver.cef_firewall");
        StringAssert.Contains(ddl, "ruleName nullable(string)");
        StringAssert.Contains(ddl, "sessionDurationMs nullable(int64)");
    }

    [TestMethod]
    public void AvroSchema_IsValidJsonWithExpectedLogicalTypes()
    {
        var avro = AvroSchemaProjection.Generate(Cef);
        using var doc = JsonDocument.Parse(avro);

        Assert.AreEqual("record", doc.RootElement.GetProperty("type").GetString());
        Assert.AreEqual("CefFirewall", doc.RootElement.GetProperty("name").GetString());

        var fields = doc.RootElement.GetProperty("fields").EnumerateArray().ToList();
        Assert.AreEqual(Cef.Entries.Count, fields.Count);

        var src = fields.Single(f => f.GetProperty("name").GetString() == "src");
        var srcType = src.GetProperty("type")[1]; // [null, <type>]
        Assert.AreEqual("ipv4", srcType.GetProperty("logicalType").GetString());

        var amount = fields.Single(f => f.GetProperty("name").GetString() == "transactionAmount");
        var amountType = amount.GetProperty("type")[1];
        Assert.AreEqual("decimal", amountType.GetProperty("logicalType").GetString());
        Assert.AreEqual(18, amountType.GetProperty("precision").GetInt32());
        Assert.AreEqual(2, amountType.GetProperty("scale").GetInt32());

        var duration = fields.Single(f => f.GetProperty("name").GetString() == "sessionDurationMs");
        var durationType = duration.GetProperty("type")[1];
        Assert.AreEqual("duration-ms", durationType.GetProperty("logicalType").GetString());
    }

    [TestMethod]
    public void ArrowSchema_IsValidJsonWithNativeDurationAndDecimal128()
    {
        var arrow = ArrowSchemaProjection.Generate(Cef);
        using var doc = JsonDocument.Parse(arrow);

        Assert.AreEqual("cef_firewall", doc.RootElement.GetProperty("name").GetString());

        var fields = doc.RootElement.GetProperty("fields").EnumerateArray().ToList();
        Assert.AreEqual(Cef.Entries.Count, fields.Count);

        var amount = fields.Single(f => f.GetProperty("name").GetString() == "transactionAmount");
        Assert.AreEqual("decimal128(18, 2)", amount.GetProperty("type").GetString());

        var duration = fields.Single(f => f.GetProperty("name").GetString() == "sessionDurationMs");
        Assert.AreEqual("duration[ms]", duration.GetProperty("type").GetString());

        var mac = fields.Single(f => f.GetProperty("name").GetString() == "smac");
        Assert.AreEqual("deltazulu.mac48", mac.GetProperty("metadata").GetProperty("ARROW:extension:name").GetString());
    }

    [TestMethod]
    public void ParserContract_EmitsInferrableSubsetOnly()
    {
        var contract = ParserContractProjection.Generate(Cef);
        using var doc = JsonDocument.Parse(contract);

        var suggestions = doc.RootElement.GetProperty("suggestedFields").EnumerateArray().ToList();
        Assert.AreEqual(Cef.Entries.Count, suggestions.Count);

        var ruleName = suggestions.Single(s => s.GetProperty("name").GetString() == "ruleName");
        Assert.IsFalse(ruleName.TryGetProperty("promoted", out _), "the Suggester's contract must not claim to infer promotion");
        Assert.IsFalse(ruleName.TryGetProperty("canonicalization", out _), "the Suggester's contract must not claim to infer canonicalization policy");
    }
}
