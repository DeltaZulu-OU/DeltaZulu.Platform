using DeltaZulu.Platform.Data.DuckDb.Sql;
using DeltaZulu.Platform.Domain.Analytics.Schema;

namespace DeltaZulu.Platform.Tests.Analytics.DuckDbSql;

[TestClass]
public sealed class SchemaEmitterCanonicalViewTests
{
    [TestMethod]
    public void EmitCanonicalView_UsesExplicitCanonicalColumnProjection()
    {
        var view = new CanonicalViewDef(
            "golden",
            "ProcessEvent",
            [
                "silver.v_processevent_windows_sysmon_eid1",
                "silver.v_processevent_windows_security_eid4688"
            ],
            [
                new ColumnDef("Timestamp", DuckDbType.Timestamp, KustoType.DateTime),
                new ColumnDef("DeviceName", DuckDbType.Varchar, KustoType.String),
                new ColumnDef("FileName", DuckDbType.Varchar, KustoType.String)
            ]);

        var sql = new SchemaEmitter().EmitCanonicalView(view);

        Assert.DoesNotContain("SELECT *", sql);
        Assert.Contains(
            "SELECT\n    Timestamp,\n    DeviceName,\n    FileName\nFROM silver.v_processevent_windows_sysmon_eid1",
            sql);
        Assert.Contains(
            "UNION ALL\nSELECT\n    Timestamp,\n    DeviceName,\n    FileName\nFROM silver.v_processevent_windows_security_eid4688",
            sql);
    }

    [TestMethod]
    public void EmitCanonicalView_PreservesDeclaredCanonicalColumnOrder()
    {
        var view = new CanonicalViewDef(
            "golden",
            "Dns",
            ["silver.v_dns_source"],
            [
                new ColumnDef("ThirdColumn", DuckDbType.Varchar, KustoType.String),
                new ColumnDef("FirstColumn", DuckDbType.Varchar, KustoType.String),
                new ColumnDef("SecondColumn", DuckDbType.Varchar, KustoType.String)
            ]);

        var sql = new SchemaEmitter().EmitCanonicalView(view);

        Assert.Contains(
            "SELECT\n    ThirdColumn,\n    FirstColumn,\n    SecondColumn\nFROM silver.v_dns_source",
            sql);
    }
}