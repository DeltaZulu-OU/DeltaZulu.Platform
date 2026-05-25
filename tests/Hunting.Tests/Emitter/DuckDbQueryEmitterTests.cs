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

    // ─── Let binding semantics ─────────────────────────────────────

    [TestMethod]
    [Description("Scalar let values are inlined inside predicate")]
    public void Let_Scalar_Inlined()
    {
        var node = new LetBindingNode(
            Name: "cutoff",
            ScalarValue: new FunctionCall("ago", [new LiteralScalar("7d", LiteralKind.Timespan)]),
            TabularValue: null,
            Body: new FilterNode(
                new ScanNode("DeviceProcessEvents"),
                new BinaryScalar(new ColumnRef("Timestamp"), ScalarBinaryOp.Gt, new ColumnRef("cutoff"))));

        var sql = _emitter.Emit(node);
        Assert.DoesNotContain(" > cutoff", NormSql(sql), "Scalar let name should not appear as bare SQL identifier");
        AssertSqlContains(sql, "current_timestamp - INTERVAL '7 days'");
    }

    [TestMethod]
    [Description("Scalar let state does not leak into subsequent emit calls")]
    public void Let_Scalar_IsolationAcrossEmits()
    {
        _emitter.Emit(new LetBindingNode(
            Name: "magic",
            ScalarValue: new LiteralScalar(42, LiteralKind.Int),
            TabularValue: null,
            Body: new ScanNode("DeviceProcessEvents")));

        var sql = _emitter.Emit(new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(new ColumnRef("magic"), ScalarBinaryOp.Gt, new LiteralScalar(0, LiteralKind.Int))));

        AssertSqlContains(sql, "magic > 0");
        Assert.DoesNotContain("42", sql, "Previous scalar let value should not leak into later query emissions");
    }

    [TestMethod]
    [Description("Tabular let creates named CTE")]
    public void Let_Tabular_EmitsNamedCte()
    {
        var node = new LetBindingNode(
            Name: "PowerShellProcs",
            TabularValue: new FilterNode(
                new ScanNode("DeviceProcessEvents"),
                new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.Eq,
                    new LiteralScalar("powershell.exe", LiteralKind.String))),
            ScalarValue: null,
            Body: new ScanNode("DeviceProcessEvents"));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "WITH");
        AssertSqlContains(sql, "PowerShellProcs");
        AssertSqlContains(sql, "powershell.exe");
    }

    [TestMethod]
    [Description("Unknown function names are rejected")]
    public void Func_Unknown_ThrowsNotSupported()
    {
        Assert.ThrowsExactly<NotSupportedException>(() =>
            _emitter.Emit(new ExtendNode(
                new ScanNode("DeviceProcessEvents"),
                [new ProjectionExpr("r", new FunctionCall("custom_function_xyz",
                    [new ColumnRef("FileName"), new LiteralScalar(42, LiteralKind.Int)]))])));
    }

    [TestMethod]
    [Description("Join ON emits full equality predicate")]
    public void Join_On_EqualityPredicate()
    {
        var node = new JoinNode(
            new ScanNode("DeviceProcessEvents"),
            new ScanNode("DeviceProcessEvents"),
            JoinKind.Inner,
            new BinaryScalar(new ColumnRef("DeviceName"), ScalarBinaryOp.Eq, new ColumnRef("DeviceName")));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "INNER JOIN");
        AssertSqlContains(sql, "ON (DeviceName = DeviceName)");
    }

    [TestMethod]
    [Description("Join output still gets default safety limit")]
    public void Join_StillGetsSafetyCap()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 100);
        var node = new JoinNode(
            new ScanNode("DeviceProcessEvents"),
            new LimitNode(new ScanNode("DeviceProcessEvents"), 5),
            JoinKind.Inner,
            new BinaryScalar(new ColumnRef("DeviceName"), ScalarBinaryOp.Eq, new ColumnRef("DeviceName")));

        var sql = emitter.Emit(node);
        AssertSqlContains(sql, "LIMIT 100");
    }



    [TestMethod]
    [Description("has_cs uses case-sensitive regex without lower()")]
    public void Op_HasCs_CaseSensitiveRegex()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(new ColumnRef("ProcessCommandLine"), ScalarBinaryOp.HasCs,
                new LiteralScalar("Mimikatz", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "regexp_matches(ProcessCommandLine");
        AssertSqlContains(sql, "'c'");
        Assert.DoesNotContain("lower(ProcessCommandLine)", NormSql(sql));
    }

    [TestMethod]
    [Description("matches regex uses regexp_matches with case-sensitive flag")]
    public void Op_MatchesRegex_UsesCaseSensitiveFlag()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(new ColumnRef("ProcessCommandLine"), ScalarBinaryOp.MatchesRegex,
                new LiteralScalar("(?i)pass(word|wd)", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "regexp_matches(ProcessCommandLine");
        AssertSqlContains(sql, "'c'");
    }



    [TestMethod]
    [Description("not has emits NOT regexp_matches with word boundary")]
    public void Op_NotHas_NegatesRegexMatch()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(new ColumnRef("ProcessCommandLine"), ScalarBinaryOp.NotHas,
                new LiteralScalar("mimikatz", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "NOT regexp_matches(lower(");
        AssertSqlContains(sql, "[^[:alnum:]]");
    }

    [TestMethod]
    [Description("hasprefix uses leading boundary only")]
    public void Op_HasPrefix_UsesLeadingBoundaryOnly()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.HasPrefix,
                new LiteralScalar("power", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "regexp_matches(lower(FileName)");
        AssertSqlContains(sql, "(^|[^[:alnum:]])");
        Assert.DoesNotContain("([^[:alnum:]]|$)", sql);
    }

    [TestMethod]
    [Description("not hasprefix negates leading-boundary regex")]
    public void Op_NotHasPrefix_NegatesLeadingBoundaryRegex()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.NotHasPrefix,
                new LiteralScalar("power", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "NOT regexp_matches(lower(FileName)");
        AssertSqlContains(sql, "(^|[^[:alnum:]])");
    }

    [TestMethod]
    [Description("hassuffix uses trailing boundary only")]
    public void Op_HasSuffix_UsesTrailingBoundaryOnly()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.HasSuffix,
                new LiteralScalar("shell", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "regexp_matches(lower(FileName)");
        AssertSqlContains(sql, "([^[:alnum:]]|$)");
        Assert.DoesNotContain("(^|[^[:alnum:]])", sql);
    }

    [TestMethod]
    [Description("hasprefix_cs uses case-sensitive flag and no lower()")]
    public void Op_HasPrefixCs_CaseSensitiveNoLowering()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.HasPrefixCs,
                new LiteralScalar("Power", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "regexp_matches(FileName");
        AssertSqlContains(sql, "'c'");
        Assert.DoesNotContain("lower(FileName)", sql);
    }

    [TestMethod]
    [Description("hassuffix_cs uses case-sensitive flag and no lower()")]
    public void Op_HasSuffixCs_CaseSensitiveNoLowering()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.HasSuffixCs,
                new LiteralScalar("Shell", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "regexp_matches(FileName");
        AssertSqlContains(sql, "'c'");
        Assert.DoesNotContain("lower(FileName)", sql);
    }

    [TestMethod]
    [Description("DistinctNode emits SELECT DISTINCT")]
    public void Tabular_Distinct_EmitsDistinctKeyword()
    {
        var node = new DistinctNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("FileName", new ColumnRef("FileName")),
             new ProjectionExpr("DeviceName", new ColumnRef("DeviceName"))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "SELECT DISTINCT FileName, DeviceName FROM");
    }

    // ─── Pipeline simplification ────────────────────────────────────

    [TestMethod]
    [Description("summarize|sort|take: no pass-through CTEs, fused ORDER BY+LIMIT, count_ has no redundant NULLS modifier")]
    public void Emit_Simplification_TopKPipeline()
    {
        var node = new LimitNode(
            new SortNode(
                new AggregateNode(
                    new ScanNode("DeviceNetworkEvents"),
                    Aggregates: [new ProjectionExpr("count_", new FunctionCall("count", []))],
                    GroupBy: [new ColumnRef("RemoteIP"), new ColumnRef("RemotePort")]),
                [new SortExpr(new ColumnRef("count_"), SortDirection.Desc)]),
            20);
        var sql = _emitter.Emit(node);
        var norm = NormSql(sql);
        var passThrough = System.Text.RegularExpressions.Regex.Matches(norm, @"AS \(SELECT \* FROM __kql_stage_\d+\)").Count;
        Assert.AreEqual(0, passThrough);
        Assert.DoesNotContain("WITH", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("__kql_stage_", norm, StringComparison.OrdinalIgnoreCase);
        AssertSqlContains(sql, "ORDER BY count_ DESC LIMIT 20");
        Assert.MatchesRegex(@"ORDER\s+BY\s+count_\s+DESC\s+LIMIT\s+20\s*;?\s*$", norm);
        Assert.DoesNotContain("NULLS LAST", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatchRegex(
            @"__kql_stage_\d+\s+AS\s*\(\s*SELECT\s+\*\s+FROM\s+main\.DeviceNetworkEvents\s*\)",
            norm);
        Assert.DoesNotMatchRegex(@"SELECT\s+\*\s+FROM\s+__kql_stage_\d+\s*;?\s*$", norm);
        AssertSqlContains(sql, "SELECT RemoteIP, RemotePort, count(*) AS count_");
        AssertSqlContains(sql, "FROM main.DeviceNetworkEvents");
        AssertSqlContains(sql, "GROUP BY RemoteIP, RemotePort");
    }

    [TestMethod]
    [Description("sort|take over base scan: terminal top-k remains outermost and does not reference removed source CTE")]
    public void Emit_Simplification_ScanSortTake_NoDanglingStageReference()
    {
        var node = new LimitNode(
            new SortNode(
                new ScanNode("DeviceNetworkEvents"),
                [new SortExpr(new ColumnRef("RemoteIP"), SortDirection.Desc)]),
            5);

        var sql = _emitter.Emit(node);
        var norm = NormSql(sql);
        Assert.MatchesRegex(@"ORDER\s+BY\s+RemoteIP\s+DESC\s+NULLS\s+LAST\s+LIMIT\s+5\s*;?\s*$", norm);
        Assert.DoesNotMatchRegex(
            @"__kql_stage_\d+\s+AS\s*\(\s*SELECT\s+\*\s+FROM\s+main\.DeviceNetworkEvents\s*\)",
            norm);
        AssertSqlContains(sql, "ORDER BY RemoteIP DESC NULLS LAST LIMIT 5");
    }

    [TestMethod]
    [Description("where|project over base scan collapses single-use filter CTE into one SELECT block")]
    public void WhereInProject_OptimizedMode_CollapsesSingleUseFilterCte()
    {
        var node = new ProjectNode(
            new FilterNode(
                new ScanNode("DeviceNetworkEvents"),
                new BinaryScalar(
                    new ColumnRef("RemotePort"),
                    ScalarBinaryOp.In,
                    new ListScalar(
                    [
                        new LiteralScalar(4444, LiteralKind.Int),
                        new LiteralScalar(1337, LiteralKind.Int),
                        new LiteralScalar(8888, LiteralKind.Int),
                        new LiteralScalar(9999, LiteralKind.Int),
                        new LiteralScalar(31337, LiteralKind.Int)
                    ]))),
            [
                new ProjectionExpr("Timestamp", new ColumnRef("Timestamp")),
                new ProjectionExpr("DeviceName", new ColumnRef("DeviceName")),
                new ProjectionExpr("LocalIP", new ColumnRef("LocalIP")),
                new ProjectionExpr("RemoteIP", new ColumnRef("RemoteIP")),
                new ProjectionExpr("RemotePort", new ColumnRef("RemotePort")),
                new ProjectionExpr("InitiatingProcessFileName", new ColumnRef("InitiatingProcessFileName"))
            ]);

        var sql = _emitter.Emit(node);
        var norm = NormSql(sql);

        Assert.DoesNotContain("WITH", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatchRegex(@"__kql_stage_\d+", norm);
        Assert.DoesNotMatchRegex(@"SELECT\s+\*", norm);
        AssertSqlContains(sql, "FROM main.DeviceNetworkEvents WHERE (RemotePort IN (4444, 1337, 8888, 9999, 31337))");
        Assert.MatchesRegex(
            @"SELECT\s+Timestamp\s*,\s*DeviceName\s*,\s*LocalIP\s*,\s*RemoteIP\s*,\s*RemotePort\s*,\s*InitiatingProcessFileName\s+FROM\s+main\.DeviceNetworkEvents",
            norm);
    }

    [TestMethod]
    [Description("where|where|project over base scan merges filters with AND and preserves OR predicate grouping")]
    public void WhereWhereProject_OptimizedMode_CollapsesAndPreservesGrouping()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 10_000, applyDefaultLimit: false);
        var node = new ProjectNode(
            new FilterNode(
                new FilterNode(
                    new ScanNode("DeviceNetworkEvents"),
                    new BinaryScalar(
                        new ColumnRef("RemotePort"),
                        ScalarBinaryOp.Eq,
                        new LiteralScalar(53, LiteralKind.Int))),
                new BinaryScalar(
                    new BinaryScalar(
                        new ColumnRef("InitiatingProcessFileName"),
                        ScalarBinaryOp.Has,
                        new LiteralScalar("powershell", LiteralKind.String)),
                    ScalarBinaryOp.Or,
                    new BinaryScalar(
                        new ColumnRef("InitiatingProcessFileName"),
                        ScalarBinaryOp.Has,
                        new LiteralScalar("cmd", LiteralKind.String)))),
            [
                new ProjectionExpr("Timestamp", new ColumnRef("Timestamp")),
                new ProjectionExpr("DeviceName", new ColumnRef("DeviceName")),
                new ProjectionExpr("RemoteUrl", new ColumnRef("RemoteUrl")),
                new ProjectionExpr("InitiatingProcessFileName", new ColumnRef("InitiatingProcessFileName")),
                new ProjectionExpr("InitiatingProcessCommandLine", new ColumnRef("InitiatingProcessCommandLine"))
            ]);

        var sql = emitter.Emit(node);
        var norm = NormSql(sql);

        Assert.DoesNotContain("WITH", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatchRegex(@"__kql_stage_\d+", norm);
        Assert.DoesNotMatchRegex(@"SELECT\s+\*", norm);
        Assert.DoesNotContain("LIMIT 10000", norm, StringComparison.OrdinalIgnoreCase);
        Assert.MatchesRegex(
            @"SELECT\s+Timestamp\s*,\s*DeviceName\s*,\s*RemoteUrl\s*,\s*InitiatingProcessFileName\s*,\s*InitiatingProcessCommandLine\s+FROM\s+main\.DeviceNetworkEvents",
            norm);
        Assert.MatchesRegex(@"WHERE\s+\(+\s*RemotePort\s*=\s*53\s*\)+\s+AND\s+\(", norm);
        Assert.MatchesRegex(@"WHERE\s+\(+\s*RemotePort\s*=\s*53\s*\)+\s+AND\s+\(+.*powershell.*\s+OR\s+.*cmd.*\)+", norm);
    }

    [TestMethod]
    [Description("Default LIMIT is opt-in; semantic mode can emit no implicit limit")]
    public void WhereInProject_SemanticMode_DoesNotAddImplicitLimit()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 10_000, applyDefaultLimit: false);
        var node = new ProjectNode(
            new FilterNode(
                new ScanNode("DeviceNetworkEvents"),
                new BinaryScalar(
                    new ColumnRef("RemotePort"),
                    ScalarBinaryOp.In,
                    new ListScalar([new LiteralScalar(4444, LiteralKind.Int), new LiteralScalar(31337, LiteralKind.Int)]))),
            [new ProjectionExpr("RemotePort", new ColumnRef("RemotePort"))]);

        var sql = emitter.Emit(node);
        var norm = NormSql(sql);
        Assert.DoesNotContain("LIMIT 10000", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatchRegex(@"\bLIMIT\b", norm);
    }

    [TestMethod]
    [Description("summarize|sort in semantic mode collapses to terminal ordered aggregate without implicit limit")]
    public void SummarizeSort_OptimizedMode_CollapsesIntoOrderedAggregateSelect()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 10_000, applyDefaultLimit: false);
        var node = new SortNode(
            new AggregateNode(
                new ScanNode("DeviceProcessEvents"),
                Aggregates: [new ProjectionExpr("count_", new FunctionCall("count", []))],
                GroupBy: [new ColumnRef("DeviceName")]),
            [new SortExpr(new ColumnRef("count_"), SortDirection.Desc)]);

        var sql = emitter.Emit(node);
        var norm = NormSql(sql);

        Assert.DoesNotContain("WITH", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatchRegex(@"__kql_stage_\d+", norm);
        Assert.DoesNotMatchRegex(@"SELECT\s+\*", norm);
        Assert.MatchesRegex(
            @"SELECT\s+DeviceName\s*,\s*count\(\*\)\s+AS\s+count_\s+FROM\s+main\.DeviceProcessEvents\s+GROUP\s+BY\s+DeviceName\s+ORDER\s+BY\s+count_\s+DESC\s*;?\s*$",
            norm);
        Assert.DoesNotContain("LIMIT 10000", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatchRegex(@"\bLIMIT\b", norm);
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
