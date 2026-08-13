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

    [TestMethod]
    public void Emit_SubstringWithTwoArgs_OmitsLengthInsteadOfThrowing()
    {
        var node = new ProjectNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("Tail", new FunctionCall("substring", [new ColumnRef("FileName"), new LiteralScalar(2L, LiteralKind.Long)]))]);

        var sql = _emitter.Emit(node).Sql;

        AssertSqlContains(sql, "substring(FileName, (2) + 1) AS Tail");
    }

    [TestMethod]
    public void Emit_SubstringWithThreeArgs_IncludesLength()
    {
        var node = new ProjectNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("Mid", new FunctionCall("substring", [new ColumnRef("FileName"), new LiteralScalar(2L, LiteralKind.Long), new LiteralScalar(4L, LiteralKind.Long)]))]);

        var sql = _emitter.Emit(node).Sql;

        AssertSqlContains(sql, "substring(FileName, (2) + 1, 4) AS Mid");
    }

    [TestMethod]
    public void Emit_ExtractCaptureGroupOne_UsesPlainExtract()
    {
        var node = new ProjectNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("Ver", new FunctionCall("extract", [
                new LiteralScalar(@"v(\d+)", LiteralKind.String),
                new LiteralScalar(1L, LiteralKind.Long),
                new ColumnRef("CommandLine")]))]);

        var sql = _emitter.Emit(node).Sql;

        AssertSqlContains(sql, @"extract(CommandLine, 'v(\d+)') AS Ver");
    }

    [TestMethod]
    public void Emit_ExtractCaptureGroupTwo_UsesExtractGroups()
    {
        var node = new ProjectNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("Ver", new FunctionCall("extract", [
                new LiteralScalar(@"(\w+)=(\d+)", LiteralKind.String),
                new LiteralScalar(2L, LiteralKind.Long),
                new ColumnRef("CommandLine")]))]);

        var sql = _emitter.Emit(node).Sql;

        AssertSqlContains(sql, @"extractGroups(CommandLine, '(\w+)=(\d+)')[2] AS Ver");
    }

    [TestMethod]
    public void Emit_ExtractCaptureGroupZero_WrapsPatternForFullMatch()
    {
        var node = new ProjectNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("Whole", new FunctionCall("extract", [
                new LiteralScalar(@"\d+", LiteralKind.String),
                new LiteralScalar(0L, LiteralKind.Long),
                new ColumnRef("CommandLine")]))]);

        var sql = _emitter.Emit(node).Sql;

        AssertSqlContains(sql, @"extract(CommandLine, '(\d+)') AS Whole");
    }

    [TestMethod]
    public void Emit_UnixtimeMillisecondsToDatetime_UsesValidToInt64FunctionName()
    {
        var node = new ProjectNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("Ts", new FunctionCall("unixtime_milliseconds_todatetime", [new ColumnRef("EpochMs")]))]);

        var sql = _emitter.Emit(node).Sql;

        Assert.DoesNotContain("to_int64", sql);
        AssertSqlContains(sql, "fromUnixTimestamp64Milli(toInt64(EpochMs)) AS Ts");
    }

    [TestMethod]
    public void Emit_UnixtimeMicrosecondsToDatetime_UsesValidToInt64FunctionName()
    {
        var node = new ProjectNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("Ts", new FunctionCall("unixtime_microseconds_todatetime", [new ColumnRef("EpochUs")]))]);

        var sql = _emitter.Emit(node).Sql;

        Assert.DoesNotContain("to_int64", sql);
        AssertSqlContains(sql, "fromUnixTimestamp64Micro(toInt64(EpochUs)) AS Ts");
    }

    [TestMethod]
    public void Emit_DatetimeAdd_WithNonLiteralPeriod_ThrowsInsteadOfEmittingInvalidSql()
    {
        var node = new ProjectNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("Later", new FunctionCall("datetime_add",
                [new ColumnRef("PeriodColumn"), new LiteralScalar(3L, LiteralKind.Long), new ColumnRef("Timestamp")]))]);

        Assert.ThrowsExactly<NotSupportedException>(() => _emitter.Emit(node));
    }

    [TestMethod]
    public void Emit_SplitWithRequestedIndex_SelectsSingleElement()
    {
        var node = new ProjectNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("Part", new FunctionCall("split", [
                new ColumnRef("FileName"),
                new LiteralScalar(".", LiteralKind.String),
                new LiteralScalar(1L, LiteralKind.Long)]))]);

        var sql = _emitter.Emit(node).Sql;

        AssertSqlContains(sql, "splitByString('.', FileName)[(1) + 1] AS Part");
    }

    [TestMethod]
    public void Emit_HasWithDynamicRightHandSide_UsesValidThreeArgRegexReplace()
    {
        var node = new ProjectNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("IsMatch", new BinaryScalar(
                new ColumnRef("CommandLine"),
                ScalarBinaryOp.Has,
                new ColumnRef("Needle")))]);

        var sql = _emitter.Emit(node).Sql;

        Assert.DoesNotContain("regexp_replace", sql);
        AssertSqlContains(sql, "replaceRegexpAll(toString(Needle), '([\\[\\](){}^$*+?.|\\\\])', '\\\\$1')");
    }

    private static void AssertSqlContains(string sql, string expected) =>
        Assert.Contains(Normalize(expected), Normalize(sql));

    private static string Normalize(string sql) =>
        string.Join(' ', sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
