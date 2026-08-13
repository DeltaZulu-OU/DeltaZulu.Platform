using DeltaZulu.Platform.Domain.Analytics.Schema;

namespace DeltaZulu.Platform.Tests.Analytics.Schema;

[TestClass]
public sealed class LogicalSchemaRegistryContractTests
{
    [TestMethod]
    public void LogicalFieldType_DefaultMappingsCoverPhase3CProjectionTargets()
    {
        var timestamp = LogicalFieldType.Timestamp(nullable: false);
        var duration = LogicalFieldType.Duration();
        var ip = LogicalFieldType.IpAddress();
        var dynamic = LogicalFieldType.Dynamic();

        AssertHasTargets(timestamp);
        AssertHasTargets(duration);
        AssertHasTargets(ip);
        AssertHasTargets(dynamic);

        Assert.AreEqual(LogicalFieldFamily.Timestamp, timestamp.Family);
        Assert.AreEqual(LogicalTimestampPrecision.Microseconds, timestamp.TimestampPrecision);
        Assert.IsFalse(timestamp.Nullable);
        Assert.AreEqual("TIMESTAMP", timestamp.BackendMappings.Single(m => m.Target == RegistryProjectionTarget.DuckDb).TypeName);
        Assert.AreEqual("datetime", timestamp.BackendMappings.Single(m => m.Target == RegistryProjectionTarget.Kql).TypeName);

        Assert.AreEqual(LogicalFieldFamily.Duration, duration.Family);
        Assert.AreEqual(LogicalDurationUnit.Microseconds, duration.DurationUnit);
        Assert.AreEqual("timespan", duration.BackendMappings.Single(m => m.Target == RegistryProjectionTarget.Kql).TypeName);

        Assert.AreEqual("ipv6", ip.BackendMappings.Single(m => m.Target == RegistryProjectionTarget.Proton).TypeName);
        Assert.AreEqual("dynamic", dynamic.BackendMappings.Single(m => m.Target == RegistryProjectionTarget.Kql).TypeName);
    }

    [TestMethod]
    public void LogicalSchemaVersion_ProvidesStableRegistryKey()
    {
        var schema = new LogicalSchemaVersion(
            "windows",
            "security-event",
            3,
            [
                new("EventTime", LogicalFieldType.Timestamp(nullable: false)),
                new("EventId", LogicalFieldType.Integer(LogicalIntegerWidth.Int32, nullable: false)),
                new("AdditionalFields", LogicalFieldType.Dynamic())
            ],
            "Windows Security event envelope.");

        Assert.AreEqual("windows/security-event/v3", schema.RegistryKey);
        Assert.HasCount(3, schema.Fields);
        Assert.AreEqual(LogicalIntegerWidth.Int32, schema.Fields[1].Type.IntegerWidth);
        Assert.AreEqual("INTEGER", schema.Fields[1].Type.BackendMappings.Single(m => m.Target == RegistryProjectionTarget.DuckDb).TypeName);
    }

    [TestMethod]
    public void Decimal_UsesExactApprovedBackendMappings()
    {
        var type = LogicalFieldType.Decimal(18, 2, nullable: false);
        Assert.AreEqual("DECIMAL(18,2)", type.BackendMappings.Single(m => m.Target == RegistryProjectionTarget.DuckDb).TypeName);
        Assert.AreEqual("decimal(18,2)", type.BackendMappings.Single(m => m.Target == RegistryProjectionTarget.Proton).TypeName);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => LogicalFieldType.Decimal(18, 19));
    }

    [TestMethod]
    public void CefFirewallProjection_PromotesFieldsAndRetainsNestedValueInDynamicBag()
    {
        var schema = BuiltInLogicalSchemas.CefFirewallV1;
        var table = LogicalSchemaProjection.ToSilverTable(schema);
        Assert.AreEqual("cef/cef_firewall/v1", schema.RegistryKey);
        Assert.IsTrue(table.Columns.Any(c => c.Name == "Extensions"));
        Assert.IsFalse(table.Columns.Any(c => c.Name == "AgentBuild"));
        var amount = table.Columns.Single(c => c.Name == "TransactionAmount");
        Assert.AreEqual(DuckDbType.Decimal, amount.DuckDbType);
        Assert.AreEqual("DECIMAL(18,2)", amount.DuckDbTypeOverride);
    }

    [TestMethod]
    public void Validator_RejectsContradictoryParserMetadata()
    {
        var schema = new LogicalSchemaVersion("cef", "invalid", 1, [new("field", LogicalFieldType.String(), Parser: new("cef:cs1", ParserFieldPlacement.TopLevel, "$.cs1"))]);
        Assert.ThrowsExactly<ArgumentException>(() => LogicalSchemaValidator.Validate(schema));
    }

    [TestMethod]
    public void AgentSinkContracts_DeriveQuackAndProtonTypesWithoutAddingTransportTarget()
    {
        var schema = BuiltInLogicalSchemas.CefFirewallV1;
        var quack = LogicalSchemaProjection.ToAgentSink(schema, AgentOutputSink.Quack);
        var proton = LogicalSchemaProjection.ToAgentSink(schema, AgentOutputSink.Proton);
        Assert.AreEqual("DECIMAL(18,2)", quack.Fields.Single(f => f.Name == "TransactionAmount").PhysicalType);
        Assert.AreEqual("decimal(18,2)", proton.Fields.Single(f => f.Name == "TransactionAmount").PhysicalType);
        Assert.AreEqual("$.Extensions.cs3", quack.Fields.Single(f => f.Name == "AgentBuild").DynamicBagPath);
    }

    private static void AssertHasTargets(LogicalFieldType type)
    {
        foreach (var target in Enum.GetValues<RegistryProjectionTarget>())
        {
            Assert.IsTrue(
                type.BackendMappings.Any(mapping => mapping.Target == target),
                $"{type.Family} is missing a {target} mapping.");
        }
    }
}
