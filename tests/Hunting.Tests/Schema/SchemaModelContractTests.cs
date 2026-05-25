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

        Assert.AreEqual("main.DeviceProcessEvents", DeviceProcessEventsSchema.View.QualifiedName);
        Assert.AreEqual("raw.windows_event_json", DeviceProcessEventsSchema.RawWindowsEventJson.QualifiedName);
        Assert.HasCount(14, DeviceProcessEventsSchema.Columns);
        Assert.HasCount(7, DeviceProcessEventsSchema.RawWindowsEventJson.Columns);
        Assert.HasCount(14, DeviceProcessEventsSchema.SysmonProcessCreate.Mapping.Projections);
    }
}