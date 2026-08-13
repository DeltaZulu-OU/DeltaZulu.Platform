using DeltaZulu.Platform.Data.DuckDb.Sql;
using DeltaZulu.Platform.Data.Proton;
using DeltaZulu.Platform.Domain.Analytics.Schema;

namespace DeltaZulu.Platform.Tests.Analytics.Schema;

[TestClass]
public sealed class LogicalSchemaProjectionEmitterTests
{
    [TestMethod]
    public void ApprovedEmitters_UseRegistryTypeOverridesWithoutParallelDdlGenerators()
    {
        var table = LogicalSchemaProjection.ToSilverTable(BuiltInLogicalSchemas.CefFirewallV1);
        var duckDb = new SchemaEmitter().EmitCreateTable(table);
        var proton = new ProtonSchemaEmitter().EmitStream(table);
        StringAssert.Contains(duckDb, "TransactionAmount DECIMAL(18,2)");
        StringAssert.Contains(proton, "TransactionAmount nullable(decimal(18,2))");
        StringAssert.Contains(proton, "EventTime nullable(datetime64(6, 'UTC'))");
        Assert.IsFalse(duckDb.Contains("AgentBuild", StringComparison.Ordinal));
        Assert.IsFalse(proton.Contains("AgentBuild", StringComparison.Ordinal));
    }
}
