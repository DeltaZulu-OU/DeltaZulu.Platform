using DeltaZulu.Platform.Data.Proton.Sql;
using DeltaZulu.Platform.Domain.Analytics.QueryModel;

namespace DeltaZulu.Platform.Tests.Analytics.Proton;

[TestClass]
public sealed class ProtonSqlQueryEmitterTests
{
    private readonly ProtonSqlQueryEmitter _emitter = new();

    [TestMethod]
    public void Emit_FilterProjectLimit_UsesSingleSelectWithoutCte()
    {
        var node = new LimitNode(
            new ProjectNode(
                new FilterNode(
                    new ScanNode("ProcessEvent"),
                    new BinaryScalar(
                        new ColumnRef("FileName"),
                        ScalarBinaryOp.Eq,
                        new LiteralScalar("powershell.exe", LiteralKind.String))),
                [
                    new ProjectionExpr("Timestamp", new ColumnRef("Timestamp")),
                    new ProjectionExpr("DeviceName", new ColumnRef("DeviceName")),
                ]),
            25);

        var sql = _emitter.Emit(node).Sql;

        Assert.DoesNotContain("WITH", Normalize(sql), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("__nrt_stage_", Normalize(sql), StringComparison.OrdinalIgnoreCase);
        AssertSqlContains(sql, "SELECT Timestamp, DeviceName FROM ProcessEvent WHERE (FileName = 'powershell.exe') LIMIT 25");
    }

    [TestMethod]
    public void Emit_FilterAggregate_UsesSingleSelectWithoutCte()
    {
        var node = new AggregateNode(
            new FilterNode(
                new ScanNode("Dns"),
                new BinaryScalar(
                    new ColumnRef("ActionType"),
                    ScalarBinaryOp.Eq,
                    new LiteralScalar("Query", LiteralKind.String))),
            Aggregates: [new ProjectionExpr("count_", new FunctionCall("count", []))],
            GroupBy: [new ColumnRef("DeviceName")]);

        var sql = _emitter.Emit(node).Sql;

        Assert.DoesNotContain("WITH", Normalize(sql), StringComparison.OrdinalIgnoreCase);
        AssertSqlContains(sql, "SELECT DeviceName, count() AS count_ FROM Dns WHERE (ActionType = 'Query') GROUP BY DeviceName");
    }

    [TestMethod]
    public void Emit_FilterAfterProject_KeepsStagingSoAliasPredicateIsValid()
    {
        var node = new FilterNode(
            new ProjectNode(
                new ScanNode("ProcessEvent"),
                [new ProjectionExpr("LowerName", new FunctionCall("tolower", [new ColumnRef("FileName")]))]),
            new BinaryScalar(
                new ColumnRef("LowerName"),
                ScalarBinaryOp.Eq,
                new LiteralScalar("cmd.exe", LiteralKind.String)));

        var sql = _emitter.Emit(node).Sql;

        AssertSqlContains(sql, "WITH");
        AssertSqlContains(sql, "lower(FileName) AS LowerName");
        AssertSqlContains(sql, "WHERE (LowerName = 'cmd.exe')");
    }

    private static void AssertSqlContains(string sql, string expected) =>
        Assert.Contains(Normalize(expected), Normalize(sql));

    private static string Normalize(string sql) =>
        string.Join(' ', sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
