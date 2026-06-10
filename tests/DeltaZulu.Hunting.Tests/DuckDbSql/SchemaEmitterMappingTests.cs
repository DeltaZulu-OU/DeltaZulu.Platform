namespace DeltaZulu.Hunting.Tests.DuckDbSql;

using DeltaZulu.Hunting.Core.DuckDbSql;
using DeltaZulu.Hunting.Core.Mapping;
using DeltaZulu.Hunting.Core.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Hunting.Core.Mapping.MapDsl;

[TestClass]
public sealed class SchemaEmitterMappingTests
{
    [TestMethod]
    public void EmitParserView_JsonExistsFilter_EmitsDuckDbJsonExists()
    {
        var view = new ParserViewDef(
            "silver",
            "v_test_json_exists",
            "test-source",
            "TestEvent",
            new MappingQueryDef(
                "bronze.test_source",
                JsonExists(Col("raw_log"), "$.EventID"),
                [
                    Map("Timestamp", Lit(null)),
                    Map("RawEventId", JsonText(Col("raw_log"), "$.EventID"))
                ]),
            [
                new ColumnDef("Timestamp", DuckDbType.Timestamp, KustoType.DateTime),
                new ColumnDef("RawEventId", DuckDbType.Varchar, KustoType.String)
            ]);

        var sql = new SchemaEmitter().EmitParserView(view);

        Assert.Contains("json_exists(raw_log, '$.EventID')", sql);
        Assert.Contains("json_extract_string(raw_log, '$.EventID') AS RawEventId", sql);
    }

    [TestMethod]
    public void EmitParserView_JsonExistsFilter_EscapesPathLiteral()
    {
        var view = new ParserViewDef(
            "silver",
            "v_test_json_exists_escape",
            "test-source",
            "TestEvent",
            new MappingQueryDef(
                "bronze.test_source",
                JsonExists(Col("raw_log"), "$.weird'path"),
                [
                    Map("RawEventId", JsonText(Col("raw_log"), "$.EventID"))
                ]),
            [
                new ColumnDef("RawEventId", DuckDbType.Varchar, KustoType.String)
            ]);

        var sql = new SchemaEmitter().EmitParserView(view);

        Assert.Contains("json_exists(raw_log, '$.weird''path')", sql);
    }

    [TestMethod]
    public void EmitParserView_TryCastProjection_EmitsDuckDbTryCast()
    {
        var view = new ParserViewDef(
            "silver",
            "v_test_try_cast",
            "test-source",
            "TestEvent",
            new MappingQueryDef(
                "bronze.test_source",
                null,
                [
                    Map("ProcessId", TryCast(JsonText(Col("raw_log"), "$.ProcessId"), DuckDbType.BigInt))
                ]),
            [
                new ColumnDef("ProcessId", DuckDbType.BigInt, KustoType.Long)
            ]);

        var sql = new SchemaEmitter().EmitParserView(view);

        Assert.Contains("TRY_CAST(json_extract_string(raw_log, '$.ProcessId') AS BIGINT) AS ProcessId", sql);
    }
}