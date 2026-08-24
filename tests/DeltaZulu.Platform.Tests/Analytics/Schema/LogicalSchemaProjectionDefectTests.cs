using DeltaZulu.Platform.Domain.Analytics.Schema;

namespace DeltaZulu.Platform.Tests.Analytics.Schema;

/// <summary>
/// Characterisation tests: these assert what the projection does <b>today</b>,
/// including where that is wrong. They exist so a known defect is visible and
/// executable rather than described in a document nobody runs, and so that fixing
/// it is a deliberate act that updates a failing assertion rather than a silent
/// behaviour change.
/// </summary>
[TestClass]
public sealed class LogicalSchemaProjectionDefectTests
{
    [TestMethod]
    public void DynamicBagFields_AreSilentlyDropped_FromSilver()
    {
        // DEFECT, live in a shipped built-in schema.
        //
        // ToSilverTable selects only fields whose placement is TopLevel. Nothing
        // routes a DynamicBag field into a bag column, and no bag column is emitted.
        // So a field declared DynamicBag appears in NO Silver column and in NO bag:
        // absence from the table is not presence in the bag.
        //
        // This is worse than an ordinary gap because it produces no null to notice.
        // The field simply does not exist downstream, and nothing errors.
        var schema = BuiltInLogicalSchemas.CefFirewallV1;

        var bagFields = schema.Fields
            .Where(f => f.Parser?.Placement == ParserFieldPlacement.DynamicBag)
            .Select(f => f.Name)
            .ToArray();

        Assert.IsTrue(bagFields.Length > 0, "Expected a built-in schema to exercise DynamicBag.");

        var table = LogicalSchemaProjection.ToSilverTable(schema);
        var columns = table.Columns.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var lost in bagFields)
        {
            Assert.IsFalse(
                columns.Contains(lost),
                $"'{lost}' now appears in Silver. If dynamic-bag routing has been " +
                "implemented, this characterisation test should be replaced by one " +
                "asserting the field is reachable through the bag.");
        }

        // Stated positively so the cost is legible: this many fields of this schema
        // are unreachable downstream.
        Assert.AreEqual(
            schema.Fields.Count(f => f.Parser?.Placement == ParserFieldPlacement.TopLevel),
            table.Columns.Count,
            "Silver column count should equal the TopLevel field count exactly, which " +
            "is precisely why the DynamicBag fields vanish.");
    }

    [TestMethod]
    public void MissingBackendMapping_IsRejectedByValidation_BeforeProjectionIsReached()
    {
        // Section 11.1 records this as an opaque "sequence contains no matching
        // element" from Single(). Measured: it is not reachable that way. The
        // validator already requires exactly one mapping per target and names the
        // field and the target, and every public entry point validates first.
        //
        // Mapping() was still given a named error, but as defence in depth for a
        // future path that skips validation - not as a fix for a live fault.
        var type = new LogicalFieldType(
            LogicalFieldFamily.String,
            Nullable: true,
            BackendMappings: [new(RegistryProjectionTarget.DuckDb, "VARCHAR")]);

        var schema = new LogicalSchemaVersion(
            ProducerFamily: "test",
            SchemaName: "missing_mapping",
            Version: 1,
            Fields: [new("Field", type, Parser: new("x", ParserFieldPlacement.TopLevel))]);

        var ex = Assert.ThrowsExactly<ArgumentException>(
            () => LogicalSchemaProjection.ToSilverTable(schema));

        StringAssert.Contains(ex.Message, "Field");
        StringAssert.Contains(ex.Message, "mapping");
    }
}
