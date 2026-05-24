namespace Hunting.Tests.Emitter;

using Hunting.Core.DuckDbSql;
using Hunting.Core.QueryModel;

/// <summary>
/// Red-green-refactor harness for RelNode → DuckDB SQL emission.
/// Each test constructs a RelNode tree directly and asserts the
/// emitted SQL string. SQL comparison normalizes whitespace.
///
/// Source: Architecture spec, KQL-to-DuckDB translation spec (Appendix I).
/// </summary>
[TestClass]
public sealed partial class DuckDbQueryEmitterTests
{
    private readonly DuckDbQueryEmitter _emitter = new(defaultLimit: 10_000);

    // ─── Basic operators ────────────────────────────────────────────

    [TestMethod]
    [Description("ScanNode emits FROM main.<view>")]
    public void Emit_Scan()
    {
        var node = new ScanNode("DeviceProcessEvents");
        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "FROM main.DeviceProcessEvents");
        AssertSqlContains(sql, "LIMIT 10000"); // default safety limit
    }

    [TestMethod]
    [Description("LimitNode wrapping ScanNode")]
    public void Emit_Limit()
    {
        var node = new LimitNode(new ScanNode("DeviceProcessEvents"), 20);
        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "LIMIT 20");
        // Should NOT have default limit when user specifies one
        Assert.DoesNotContain("LIMIT 10000", NormSql(sql), "Default limit should not appear when user specifies LIMIT");
    }

    [TestMethod]
    [Description("FilterNode with string equality")]
    public void Emit_Filter_StringEq()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new ColumnRef("FileName"),
                ScalarBinaryOp.Eq,
                new LiteralScalar("powershell.exe", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "FileName = 'powershell.exe'");
        AssertSqlContains(sql, "main.DeviceProcessEvents");
    }

    [TestMethod]
    [Description("ProjectNode selects named columns")]
    public void Emit_Project()
    {
        var node = new ProjectNode(
            new ScanNode("DeviceProcessEvents"),
            [
                new ProjectionExpr("Timestamp", new ColumnRef("Timestamp")),
                new ProjectionExpr("DeviceName", new ColumnRef("DeviceName")),
            ]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "SELECT Timestamp, DeviceName FROM");
    }

    [TestMethod]
    [Description("SortNode with desc direction")]
    public void Emit_Sort_Desc()
    {
        var node = new SortNode(
            new ScanNode("DeviceProcessEvents"),
            [new SortExpr(new ColumnRef("Timestamp"), SortDirection.Desc)]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "ORDER BY Timestamp DESC NULLS LAST");
    }

    // ─── Extend (subquery nesting) ──────────────────────────────────

    [TestMethod]
    [Description("ExtendNode adds computed column via SELECT *, expr AS alias")]
    public void Emit_Extend()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("lower_name",
                new FunctionCall("tolower", [new ColumnRef("FileName")]))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "lower(FileName) AS lower_name");
        AssertSqlContains(sql, "SELECT *,");
    }

    [TestMethod]
    [Description("Chained ExtendNodes produce staged CTEs")]
    public void Emit_Extend_Chained()
    {
        var inner = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("lower_name",
                new FunctionCall("tolower", [new ColumnRef("FileName")]))]);

        var outer = new ExtendNode(
            inner,
            [new ProjectionExpr("name_len",
                new FunctionCall("strlen", [new ColumnRef("lower_name")]))]);

        var sql = _emitter.Emit(outer);
        AssertSqlContains(sql, "lower(FileName) AS lower_name");
        AssertSqlContains(sql, "length(lower_name) AS name_len");
        // Must have CTEs for staging
        AssertSqlContains(sql, "WITH");
        AssertSqlContains(sql, "__kql_stage_");
    }

    // ─── Aggregate ──────────────────────────────────────────────────

    [TestMethod]
    [Description("AggregateNode with count() by")]
    public void Emit_Aggregate_CountBy()
    {
        var node = new AggregateNode(
            new ScanNode("DeviceProcessEvents"),
            Aggregates: [new ProjectionExpr("count_", new FunctionCall("count", []))],
            GroupBy: [new ColumnRef("FileName")]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "count(*) AS count_");
        AssertSqlContains(sql, "GROUP BY FileName");
    }

    // ─── Function mapping ───────────────────────────────────────────

    [TestMethod]
    [Description("ago(7d) emits current_timestamp - INTERVAL (ago() not in official DuckDB docs v1.5)")]
    public void Emit_Func_Ago()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new ColumnRef("Timestamp"),
                ScalarBinaryOp.Gt,
                new FunctionCall("ago", [new LiteralScalar("7d", LiteralKind.Timespan)])));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "current_timestamp - INTERVAL '7 days'");
        // ago() must NOT be emitted — it is not in official DuckDB documentation
        Assert.DoesNotContain("ago(", NormSql(sql),
            $"ago() is not a documented DuckDB function. Got: {NormSql(sql)}");
    }

    [TestMethod]
    [Description("tolower → lower, strlen → length")]
    public void Emit_Func_StringMappings()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [
                new ProjectionExpr("a", new FunctionCall("tolower", [new ColumnRef("FileName")])),
                new ProjectionExpr("b", new FunctionCall("strlen", [new ColumnRef("FileName")])),
            ]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "lower(FileName) AS a");
        AssertSqlContains(sql, "length(FileName) AS b");
    }

    // ─── Window functions ────────────────────────────────────────

    [TestMethod]
    [Description("WindowScalarExpr(lag) → lag(...) OVER (ORDER BY ...)")]
    public void Emit_Window_Lag()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("prev_ts",
                new WindowScalarExpr(
                    "lag",
                    [new ColumnRef("Timestamp")],
                    new WindowSpec(
                        PartitionBy: [],
                        OrderBy: [new SortExpr(new ColumnRef("Timestamp"), SortDirection.Asc)])))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "lag(Timestamp) OVER (ORDER BY Timestamp ASC NULLS FIRST)");
    }

    [TestMethod]
    [Description("WindowScalarExpr(lead) → lead(...) OVER (ORDER BY ...)")]
    public void Emit_Window_Lead()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("next_ts",
                new WindowScalarExpr(
                    "lead",
                    [new ColumnRef("Timestamp")],
                    new WindowSpec(
                        PartitionBy: [],
                        OrderBy: [new SortExpr(new ColumnRef("Timestamp"), SortDirection.Asc)])))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "lead(Timestamp) OVER (ORDER BY Timestamp ASC NULLS FIRST)");
    }

    [TestMethod]
    [Description("WindowScalarExpr(row_number) → row_number() OVER (ORDER BY ...)")]
    public void Emit_Window_RowNumber()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("rn",
                new WindowScalarExpr(
                    "row_number",
                    [],
                    new WindowSpec(
                        PartitionBy: [],
                        OrderBy: [new SortExpr(new ColumnRef("Timestamp"), SortDirection.Asc)])))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "row_number() OVER (ORDER BY Timestamp ASC NULLS FIRST)");
    }

    [TestMethod]
    [Description("WindowScalarExpr(sum) with ROWS UNBOUNDED PRECEDING → cumulative sum")]
    public void Emit_Window_CumulativeSum()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("running_total",
                new WindowScalarExpr(
                    "sum",
                    [new ColumnRef("event_count")],
                    new WindowSpec(
                        PartitionBy: [],
                        OrderBy: [new SortExpr(new ColumnRef("Timestamp"), SortDirection.Asc)],
                        Frame: new WindowFrame(
                            WindowFrameType.Rows,
                            new WindowBound(WindowBoundKind.UnboundedPreceding),
                            new WindowBound(WindowBoundKind.CurrentRow)))))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "sum(event_count) OVER (ORDER BY Timestamp ASC NULLS FIRST ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)");
    }

    [TestMethod]
    [Description("WindowScalarExpr with PARTITION BY")]
    public void Emit_Window_WithPartition()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("rn_per_device",
                new WindowScalarExpr(
                    "row_number",
                    [],
                    new WindowSpec(
                        PartitionBy: [new ColumnRef("DeviceName")],
                        OrderBy: [new SortExpr(new ColumnRef("Timestamp"), SortDirection.Asc)])))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "PARTITION BY DeviceName ORDER BY Timestamp ASC NULLS FIRST");
    }

    [TestMethod]
    [Description("Sliding window: RANGE BETWEEN INTERVAL PRECEDING AND CURRENT ROW")]
    public void Emit_Window_RangeFrame()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("events_last_15m",
                new WindowScalarExpr(
                    "count",
                    [],
                    new WindowSpec(
                        PartitionBy: [],
                        OrderBy: [new SortExpr(new ColumnRef("Timestamp"), SortDirection.Asc)],
                        Frame: new WindowFrame(
                            WindowFrameType.Range,
                            new WindowBound(WindowBoundKind.Preceding,
                                new LiteralScalar("15m", LiteralKind.Timespan)),
                            new WindowBound(WindowBoundKind.CurrentRow)))))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "RANGE BETWEEN INTERVAL '15 minutes' PRECEDING AND CURRENT ROW");
    }

    // ─── Spec-derived: CTE staging ──────────────────────────────────

    [TestMethod]
    [Description("Multi-stage pipeline emits CTE chain")]
    public void Emit_CteStaging_MultiStage()
    {
        var node =
            new LimitNode(
                new ProjectNode(
                    new FilterNode(
                        new ScanNode("DeviceProcessEvents"),
                        new BinaryScalar(
                            new ColumnRef("FileName"),
                            ScalarBinaryOp.Eq,
                            new LiteralScalar("cmd.exe", LiteralKind.String))),
                    [
                        new ProjectionExpr("Timestamp", new ColumnRef("Timestamp")),
                        new ProjectionExpr("DeviceName", new ColumnRef("DeviceName")),
                    ]),
                10);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "WITH");
        AssertSqlContains(sql, "__kql_stage_");
        AssertSqlContains(sql, "FileName = 'cmd.exe'");
        AssertSqlContains(sql, "LIMIT 10");
    }

    // ─── Spec-derived: sort default direction ────────────────────────

    [TestMethod]
    [Description("KQL sort default is desc; emitter must emit DESC explicitly")]
    public void Emit_Sort_KqlDefaultDesc()
    {
        var node = new SortNode(
            new ScanNode("DeviceProcessEvents"),
            [new SortExpr(new ColumnRef("Timestamp"), SortDirection.Desc)]);

        var sql = _emitter.Emit(node);
        // Must contain explicit DESC — never rely on DuckDB default (ASC)
        AssertSqlContains(sql, "ORDER BY Timestamp DESC NULLS LAST");
    }

    // ─── Spec-derived: extract COALESCE wrapping ─────────────────────

    [TestMethod]
    [Description("extract wraps with COALESCE to preserve KQL empty-string-on-no-match")]
    public void Emit_Func_ExtractCoalesceWrap()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("user",
                new FunctionCall("extract",
                    [new LiteralScalar(@"User=(\w+)", LiteralKind.String),
                     new LiteralScalar(1, LiteralKind.Int),
                     new ColumnRef("ProcessCommandLine")]))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "COALESCE(regexp_extract(");
    }

    // ─── Composed pipeline ──────────────────────────────────────────

    [TestMethod]
    [Description("Full vertical slice: filter → project → limit")]
    public void Emit_Composed_VerticalSlice()
    {
        var node =
            new LimitNode(
                new ProjectNode(
                    new FilterNode(
                        new ScanNode("DeviceProcessEvents"),
                        new BinaryScalar(
                            new ColumnRef("FileName"),
                            ScalarBinaryOp.Eq,
                            new LiteralScalar("powershell.exe", LiteralKind.String))),
                    [
                        new ProjectionExpr("Timestamp", new ColumnRef("Timestamp")),
                        new ProjectionExpr("DeviceName", new ColumnRef("DeviceName")),
                        new ProjectionExpr("ProcessCommandLine", new ColumnRef("ProcessCommandLine")),
                    ]),
                20);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "FileName = 'powershell.exe'");
        AssertSqlContains(sql, "Timestamp, DeviceName, ProcessCommandLine");
        AssertSqlContains(sql, "LIMIT 20");
        AssertSqlContains(sql, "main.DeviceProcessEvents");
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private static string NormSql(string s) =>
        MyRegex().Replace(s.Trim(), " ");

    private static void AssertSqlContains(string sql, string fragment)
    {
        var norm = NormSql(sql);
        var normFrag = NormSql(fragment);
        Assert.Contains(normFrag, norm,
            $"Expected SQL to contain '{normFrag}'\nActual: {norm}");
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\s+")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();
}
