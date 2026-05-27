using Hunting.Core.Schema;
using Hunting.Schema.Definitions;

namespace Hunting.Tests.Schema;

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

        Assert.AreEqual("golden.ProcessEvents", ProcessEvents.View.QualifiedName);
        Assert.AreEqual("bronze.windows_event_json", ProcessEvents.RawWindowsEventJson.QualifiedName);
        Assert.HasCount(14, ProcessEvents.Columns);
        Assert.HasCount(7, ProcessEvents.RawWindowsEventJson.Columns);
        Assert.HasCount(14, ProcessEvents.SysmonProcessCreate.Mapping.Projections);
    }
}