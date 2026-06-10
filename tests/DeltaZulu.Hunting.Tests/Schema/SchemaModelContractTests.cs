using DeltaZulu.Hunting.Core.Schema;
using DeltaZulu.Hunting.Schema;

namespace DeltaZulu.Hunting.Tests.Schema;

[TestClass]
public class SchemaModelContractTests
{
    [TestMethod]
    public void SchemaModel_TypeMappingsAndShapeMatchContract()
    {
        Assert.AreEqual("VARCHAR", DuckDbType.Varchar.ToSql());
        Assert.AreEqual("BIGINT", DuckDbType.BigInt.ToSql());
        Assert.AreEqual("TIMESTAMP", DuckDbType.Timestamp.ToSql());
        Assert.AreEqual("JSON", DuckDbType.Json.ToSql());

        Assert.AreEqual("string", KustoType.String.ToKustoName());
        Assert.AreEqual("datetime", KustoType.DateTime.ToKustoName());
        Assert.AreEqual("dynamic", KustoType.Dynamic.ToKustoName());

        Assert.AreEqual(DuckDbType.Varchar, KustoType.String.ToDefaultDuckDbType());
        Assert.AreEqual(DuckDbType.Json, KustoType.Dynamic.ToDefaultDuckDbType());
        Assert.AreEqual(DuckDbType.BigInt, KustoType.Timespan.ToDefaultDuckDbType());

        Assert.AreEqual("bronze.windows_sysmon_event", SchemaConventions.RawTables.Single(t => t.Name == "windows_sysmon_event").QualifiedName);
        Assert.AreEqual("bronze.windows_security_event", SchemaConventions.RawTables.Single(t => t.Name == "windows_security_event").QualifiedName);
        Assert.AreEqual("bronze.dns_server_event", SchemaConventions.RawTables.Single(t => t.Name == "dns_server_event").QualifiedName);

        Assert.AreEqual("golden.ProcessEvent", SchemaConventions.CanonicalViews.Single(v => v.Name == "ProcessEvent").QualifiedName);
        Assert.AreEqual("golden.NetworkSession", SchemaConventions.CanonicalViews.Single(v => v.Name == "NetworkSession").QualifiedName);
        Assert.AreEqual("golden.Dns", SchemaConventions.CanonicalViews.Single(v => v.Name == "Dns").QualifiedName);

        Assert.HasCount(3, SchemaConventions.RawTables);
        Assert.HasCount(6, SchemaConventions.ParserViews);
        Assert.HasCount(3, SchemaConventions.CanonicalViews);
    }
}