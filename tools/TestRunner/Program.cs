using System.Text.RegularExpressions;
using Hunting.Core.DuckDbSql;
using Hunting.Core.QueryModel;
using Hunting.Core.Schema;
using Hunting.Core.Schema.Definitions;
using Hunting.Core.Mapping;

int passed = 0, failed = 0, total = 0;
var failures = new List<(string Category, string Test, string Error)>();

void Assert(bool condition, string test, string category, string detail = "")
{
    total++;
    if (condition) { passed++; }
    else { failed++; failures.Add((category, test, detail)); }
}

void AssertEq<T>(T expected, T actual, string test, string category) where T : notnull
{
    Assert(expected.Equals(actual), test, category, $"Expected: {expected}, Actual: {actual}");
}

string Norm(string s) => Regex.Replace(s.Trim(), @"\s+", " ");

void AssertContains(string sql, string fragment, string test, string category)
{
    Assert(Norm(sql).Contains(Norm(fragment)), test, category,
        $"SQL missing '{Norm(fragment)}'\nActual: {Norm(sql)}");
}

void AssertNotContains(string sql, string fragment, string test, string category)
{
    Assert(!Norm(sql).Contains(Norm(fragment)), test, category,
        $"SQL should NOT contain '{Norm(fragment)}'\nActual: {Norm(sql)}");
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 1: Schema Model Types
// ════════════════════════════════════════════════════════════════════

var cat = "SchemaModel";

AssertEq("VARCHAR", DuckDbType.Varchar.ToSql(), "DuckDbType.Varchar.ToSql()", cat);
AssertEq("BIGINT", DuckDbType.BigInt.ToSql(), "DuckDbType.BigInt.ToSql()", cat);
AssertEq("TIMESTAMP", DuckDbType.Timestamp.ToSql(), "DuckDbType.Timestamp.ToSql()", cat);
AssertEq("JSON", DuckDbType.Json.ToSql(), "DuckDbType.Json.ToSql()", cat);

AssertEq("string", KustoType.String.ToKustoName(), "KustoType.String.ToKustoName()", cat);
AssertEq("datetime", KustoType.DateTime.ToKustoName(), "KustoType.DateTime.ToKustoName()", cat);
AssertEq("dynamic", KustoType.Dynamic.ToKustoName(), "KustoType.Dynamic.ToKustoName()", cat);

AssertEq(DuckDbType.Varchar, KustoType.String.ToDefaultDuckDbType(), "String→Varchar", cat);
AssertEq(DuckDbType.Json, KustoType.Dynamic.ToDefaultDuckDbType(), "Dynamic→Json", cat);
AssertEq(DuckDbType.BigInt, KustoType.Timespan.ToDefaultDuckDbType(), "Timespan→BigInt", cat);

AssertEq("main.DeviceProcessEvents", DeviceProcessEventsSchema.View.QualifiedName, "View.QualifiedName", cat);
AssertEq("raw.windows_event_json", DeviceProcessEventsSchema.RawWindowsEventJson.QualifiedName, "Raw.QualifiedName", cat);
AssertEq(14, DeviceProcessEventsSchema.Columns.Count, "Canonical column count", cat);
AssertEq(7, DeviceProcessEventsSchema.RawWindowsEventJson.Columns.Count, "Raw column count", cat);
AssertEq(14, DeviceProcessEventsSchema.SysmonProcessCreate.Mapping.Projections.Count, "Parser projection count", cat);

// ════════════════════════════════════════════════════════════════════
// CATEGORY 2: Emitter — Basic Operators
// ════════════════════════════════════════════════════════════════════

cat = "Emitter.BasicOps";
var emitter = new DuckDbQueryEmitter(defaultLimit: 10_000);

{ // Scan
    var sql = emitter.Emit(new ScanNode("DeviceProcessEvents"));
    AssertContains(sql, "FROM main.DeviceProcessEvents", "Scan_FROM", cat);
    AssertContains(sql, "LIMIT 10000", "Scan_DefaultLimit", cat);
}

{ // Limit
    var sql = emitter.Emit(new LimitNode(new ScanNode("DeviceProcessEvents"), 20));
    AssertContains(sql, "LIMIT 20", "Limit_UserValue", cat);
    AssertNotContains(sql, "LIMIT 10000", "Limit_NoDefault", cat);
}

{ // Filter
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.Eq, new LiteralScalar("cmd.exe", LiteralKind.String))));
    AssertContains(sql, "FileName = 'cmd.exe'", "Filter_StringEq", cat);
}

{ // Project
    var sql = emitter.Emit(new ProjectNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("Timestamp", new ColumnRef("Timestamp")),
         new ProjectionExpr("DeviceName", new ColumnRef("DeviceName"))]));
    AssertContains(sql, "SELECT Timestamp, DeviceName FROM", "Project_Columns", cat);
}

{ // Sort DESC — KQL default is DESC NULLS LAST (spec §11.3)
    var sql = emitter.Emit(new SortNode(
        new ScanNode("DeviceProcessEvents"),
        [new SortExpr(new ColumnRef("Timestamp"), SortDirection.Desc)]));
    AssertContains(sql, "ORDER BY Timestamp DESC NULLS LAST", "Sort_Desc_NullsLast", cat);
}

{ // Sort ASC — KQL default is ASC NULLS FIRST (spec §11.3)
    var sql = emitter.Emit(new SortNode(
        new ScanNode("DeviceProcessEvents"),
        [new SortExpr(new ColumnRef("Timestamp"), SortDirection.Asc)]));
    AssertContains(sql, "ORDER BY Timestamp ASC NULLS FIRST", "Sort_Asc_NullsFirst", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 3: Emitter — Extend / CTE Staging
// ════════════════════════════════════════════════════════════════════

cat = "Emitter.Extend";

{ // Single extend
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("lower_name", new FunctionCall("tolower", [new ColumnRef("FileName")]))]));
    AssertContains(sql, "lower(FileName) AS lower_name", "Extend_FunctionMapping", cat);
    AssertContains(sql, "SELECT *,", "Extend_StarComma", cat);
}

{ // Chained extend
    var inner = new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("lower_name", new FunctionCall("tolower", [new ColumnRef("FileName")]))]);
    var outer = new ExtendNode(inner,
        [new ProjectionExpr("name_len", new FunctionCall("strlen", [new ColumnRef("lower_name")]))]);
    var sql = emitter.Emit(outer);
    AssertContains(sql, "lower(FileName) AS lower_name", "ChainedExtend_Inner", cat);
    AssertContains(sql, "length(lower_name) AS name_len", "ChainedExtend_Outer", cat);
    AssertContains(sql, "WITH", "ChainedExtend_CTE", cat);
    AssertContains(sql, "__kql_stage_", "ChainedExtend_StageNames", cat);
}

{ // Multi-stage pipeline CTE
    var node = new LimitNode(
        new ProjectNode(
            new FilterNode(
                new ScanNode("DeviceProcessEvents"),
                new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.Eq, new LiteralScalar("cmd.exe", LiteralKind.String))),
            [new ProjectionExpr("Timestamp", new ColumnRef("Timestamp")),
             new ProjectionExpr("DeviceName", new ColumnRef("DeviceName"))]),
        10);
    var sql = emitter.Emit(node);
    AssertContains(sql, "WITH", "CTE_Present", cat);
    AssertContains(sql, "LIMIT 10", "CTE_Limit", cat);
    AssertContains(sql, "FileName = 'cmd.exe'", "CTE_Filter", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 4: Emitter — Aggregation
// ════════════════════════════════════════════════════════════════════

cat = "Emitter.Aggregate";

{ // count() by
    var sql = emitter.Emit(new AggregateNode(
        new ScanNode("DeviceProcessEvents"),
        Aggregates: [new ProjectionExpr("count_", new FunctionCall("count", []))],
        GroupBy: [new ColumnRef("FileName")]));
    AssertContains(sql, "count(*) AS count_", "Agg_CountStar", cat);
    AssertContains(sql, "GROUP BY FileName", "Agg_GroupBy", cat);
}

{ // Global aggregate (no GROUP BY)
    var sql = emitter.Emit(new AggregateNode(
        new ScanNode("DeviceProcessEvents"),
        Aggregates: [new ProjectionExpr("total", new FunctionCall("count", []))],
        GroupBy: []));
    AssertContains(sql, "count(*) AS total", "Agg_GlobalCount", cat);
    AssertNotContains(sql, "GROUP BY", "Agg_NoGroupBy", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 5: Emitter — Function Mappings
// ════════════════════════════════════════════════════════════════════

cat = "Emitter.Functions";

{ // ago — emits current_timestamp - INTERVAL (ago() is NOT in official DuckDB docs)
    // Verified: https://duckdb.org/docs/current/sql/functions/timestamp (v1.5, May 2026)
    // The documented DuckDB idiom is: current_timestamp - INTERVAL '7 days'
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("Timestamp"), ScalarBinaryOp.Gt,
            new FunctionCall("ago", [new LiteralScalar("7d", LiteralKind.Timespan)]))));
    AssertContains(sql, "current_timestamp - INTERVAL '7 days'", "Func_Ago", cat);
    AssertNotContains(sql, "ago(", "Func_Ago_NoNativeAgo", cat);
}

{ // extract wraps COALESCE
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("user", new FunctionCall("extract",
            [new LiteralScalar(@"User=(\w+)", LiteralKind.String),
             new LiteralScalar(1, LiteralKind.Int),
             new ColumnRef("ProcessCommandLine")]))]));
    AssertContains(sql, "COALESCE(regexp_extract(", "Func_ExtractCoalesce", cat);
}

{ // isempty
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new FunctionCall("isempty", [new ColumnRef("FileName")])));
    AssertContains(sql, "IS NULL OR", "Func_IsEmpty_Null", cat);
    AssertContains(sql, "= ''", "Func_IsEmpty_Empty", cat);
}

{ // coalesce
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("safe", new FunctionCall("coalesce",
            [new ColumnRef("DeviceName"), new LiteralScalar("unknown", LiteralKind.String)]))]));
    AssertContains(sql, "COALESCE(DeviceName, 'unknown')", "Func_Coalesce", cat);
}

{ // tolower, strlen
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("a", new FunctionCall("tolower", [new ColumnRef("FileName")])),
         new ProjectionExpr("b", new FunctionCall("strlen", [new ColumnRef("FileName")]))]));
    AssertContains(sql, "lower(FileName) AS a", "Func_ToLower", cat);
    AssertContains(sql, "length(FileName) AS b", "Func_StrLen", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 6: Emitter — Literals and Escaping
// ════════════════════════════════════════════════════════════════════

cat = "Emitter.Literals";

{ // NULL
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("empty_col", new LiteralScalar(null, LiteralKind.Null))]));
    AssertContains(sql, "NULL AS empty_col", "Lit_Null", cat);
}

{ // Boolean true/false
    var sql1 = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("flag", new LiteralScalar(true, LiteralKind.Bool))]));
    AssertContains(sql1, "TRUE AS flag", "Lit_BoolTrue", cat);

    var sql2 = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("flag", new LiteralScalar(false, LiteralKind.Bool))]));
    AssertContains(sql2, "FALSE AS flag", "Lit_BoolFalse", cat);
}

{ // Single quote escaping (SQL injection prevention)
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.Eq,
            new LiteralScalar("it's", LiteralKind.String))));
    AssertContains(sql, "'it''s'", "Lit_QuoteEscape", cat);
}

{ // Backslash preserved
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("FolderPath"), ScalarBinaryOp.Eq,
            new LiteralScalar(@"C:\Windows\cmd.exe", LiteralKind.String))));
    AssertContains(sql, @"C:\Windows\cmd.exe", "Lit_Backslash", cat);
}

{ // Timespan variants — all produce current_timestamp - INTERVAL '...'
    void TestTs(string input, string expectedInterval, string name)
    {
        var sql = emitter.Emit(new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(new ColumnRef("Timestamp"), ScalarBinaryOp.Gt,
                new FunctionCall("ago", [new LiteralScalar(input, LiteralKind.Timespan)]))));
        AssertContains(sql, expectedInterval, name, cat);
        AssertContains(sql, "current_timestamp -", name + "_prefix", cat);
    }
    TestTs("1d",    "INTERVAL '1 days'",           "Ts_Days");
    TestTs("2h",    "INTERVAL '2 hours'",           "Ts_Hours");
    TestTs("30m",   "INTERVAL '30 minutes'",        "Ts_Minutes");
    TestTs("10s",   "INTERVAL '10 seconds'",        "Ts_Seconds");
    TestTs("500ms", "INTERVAL '500 milliseconds'",  "Ts_Milliseconds");
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 7: Emitter — String Operators (cs vs ci)
// ════════════════════════════════════════════════════════════════════

cat = "Emitter.StringOps";

{ // contains → ILIKE with ESCAPE clause (metachar safety)
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("ProcessCommandLine"), ScalarBinaryOp.Contains,
            new LiteralScalar("password", LiteralKind.String))));
    AssertContains(sql, "ILIKE", "Contains_ILIKE", cat);
    AssertContains(sql, "ESCAPE", "Contains_ESCAPE_clause", cat);
}

{ // contains_cs → LIKE (case-sensitive)
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("ProcessCommandLine"), ScalarBinaryOp.ContainsCs,
            new LiteralScalar("Password", LiteralKind.String))));
    var norm = Norm(sql);
    Assert(norm.Contains(" LIKE ") && !norm.Contains("ILIKE"), "ContainsCs_LIKE", cat,
        $"Expected LIKE (not ILIKE) in: {norm}");
}

{ // startswith → ILIKE (case-insensitive)
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.StartsWith,
            new LiteralScalar("power", LiteralKind.String))));
    AssertContains(sql, "ILIKE", "StartsWith_ILIKE", cat);
}

{ // startswith_cs → LIKE (case-sensitive)
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.StartsWithCs,
            new LiteralScalar("Power", LiteralKind.String))));
    var norm = Norm(sql);
    Assert(norm.Contains(" LIKE ") && !norm.Contains("ILIKE"), "StartsWithCs_LIKE", cat,
        $"Expected LIKE (not ILIKE) in: {norm}");
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 8: Emitter — Window Functions
// ════════════════════════════════════════════════════════════════════

cat = "Emitter.Window";

{ // lag
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("prev_ts",
            new WindowScalarExpr("lag", [new ColumnRef("Timestamp")],
                new WindowSpec([], [new SortExpr(new ColumnRef("Timestamp"), SortDirection.Asc)])))]));
    AssertContains(sql, "lag(Timestamp) OVER (ORDER BY Timestamp ASC NULLS FIRST)", "Win_Lag", cat);
}

{ // row_number
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("rn",
            new WindowScalarExpr("row_number", [],
                new WindowSpec([], [new SortExpr(new ColumnRef("Timestamp"), SortDirection.Asc)])))]));
    AssertContains(sql, "row_number() OVER (ORDER BY Timestamp ASC NULLS FIRST)", "Win_RowNumber", cat);
}

{ // cumulative sum with frame
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("running",
            new WindowScalarExpr("sum", [new ColumnRef("val")],
                new WindowSpec([], [new SortExpr(new ColumnRef("Timestamp"), SortDirection.Asc)],
                    new WindowFrame(WindowFrameType.Rows,
                        new WindowBound(WindowBoundKind.UnboundedPreceding),
                        new WindowBound(WindowBoundKind.CurrentRow)))))]));
    AssertContains(sql, "ORDER BY Timestamp ASC NULLS FIRST ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW", "Win_CumSum", cat);
}

{ // partition by
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("rn",
            new WindowScalarExpr("row_number", [],
                new WindowSpec([new ColumnRef("DeviceName")],
                    [new SortExpr(new ColumnRef("Timestamp"), SortDirection.Asc)])))]));
    AssertContains(sql, "PARTITION BY DeviceName ORDER BY Timestamp ASC NULLS FIRST", "Win_Partition", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 9: Emitter — Joins
// ════════════════════════════════════════════════════════════════════

cat = "Emitter.Joins";

{ // SEMI JOIN
    var sql = emitter.Emit(new JoinNode(
        new ScanNode("DeviceProcessEvents"), new ScanNode("DeviceProcessEvents"),
        JoinKind.LeftSemi,
        new BinaryScalar(new ColumnRef("DeviceName"), ScalarBinaryOp.Eq, new ColumnRef("DeviceName"))));
    AssertContains(sql, "SEMI JOIN", "Join_Semi", cat);
}

{ // ANTI JOIN
    var sql = emitter.Emit(new JoinNode(
        new ScanNode("DeviceProcessEvents"), new ScanNode("DeviceProcessEvents"),
        JoinKind.LeftAnti,
        new BinaryScalar(new ColumnRef("DeviceName"), ScalarBinaryOp.Eq, new ColumnRef("DeviceName"))));
    AssertContains(sql, "ANTI JOIN", "Join_Anti", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 10: Emitter — Identifier Quoting
// ════════════════════════════════════════════════════════════════════

cat = "Emitter.Identifiers";

AssertEq("Timestamp", DuckDbQueryEmitter.EscapeIdent("Timestamp"), "Ident_Normal", cat);
AssertEq("\"Device Name\"", DuckDbQueryEmitter.EscapeIdent("Device Name"), "Ident_Space", cat);
AssertEq("\"123col\"", DuckDbQueryEmitter.EscapeIdent("123col"), "Ident_DigitStart", cat);
AssertEq("\"\"", DuckDbQueryEmitter.EscapeIdent(""), "Ident_Empty", cat);
AssertEq("col_name", DuckDbQueryEmitter.EscapeIdent("col_name"), "Ident_Underscore", cat);

// ════════════════════════════════════════════════════════════════════
// CATEGORY 11: Emitter — Edge Cases
// ════════════════════════════════════════════════════════════════════

cat = "Emitter.EdgeCases";

{ // LIMIT 0
    var sql = emitter.Emit(new LimitNode(new ScanNode("DeviceProcessEvents"), 0));
    AssertContains(sql, "LIMIT 0", "Edge_LimitZero", cat);
}

{ // LIMIT 1
    var sql = emitter.Emit(new LimitNode(new ScanNode("DeviceProcessEvents"), 1));
    AssertContains(sql, "LIMIT 1", "Edge_LimitOne", cat);
}

{ // No double LIMIT
    var sql = emitter.Emit(new LimitNode(new ScanNode("DeviceProcessEvents"), 50));
    var limitCount = Regex.Matches(Norm(sql), @"\bLIMIT\b").Count;
    AssertEq(1, limitCount, "Edge_NoDoubleLIMIT", cat);
}

{ // Deep pipeline
    RelNode node = new ScanNode("DeviceProcessEvents");
    for (int i = 0; i < 5; i++)
        node = new FilterNode(node, new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Gt, new LiteralScalar(i, LiteralKind.Int)));
    node = new LimitNode(node, 10);
    var sql = emitter.Emit(node);
    var stageCount = Regex.Matches(sql, @"__kql_stage_\d+").Count;
    Assert(stageCount >= 6, "Edge_DeepPipeline", cat, $"Expected 6+ stages, got {stageCount}");
}

{ // Emitter reuse — consistent output
    var e2 = new DuckDbQueryEmitter(defaultLimit: 100);
    var node = new LimitNode(new ScanNode("DeviceProcessEvents"), 5);
    var sql1 = e2.Emit(node);
    var sql2 = e2.Emit(node);
    AssertEq(Norm(sql1), Norm(sql2), "Edge_ReuseConsistent", cat);
}

{ // Emitter reuse — stage counter resets
    var e2 = new DuckDbQueryEmitter(defaultLimit: 100);
    var deep = new LimitNode(
        new FilterNode(new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Gt, new LiteralScalar(0, LiteralKind.Int))),
        10);
    e2.Emit(deep);
    var simple = new ScanNode("DeviceProcessEvents");
    var sql2 = e2.Emit(simple);
    Assert(!sql2.Contains("__kql_stage_5"), "Edge_StageCounterResets", cat);
}

{ // Unsupported RelNode type
    bool threw = false;
    try { emitter.Emit(new UnsupportedTestNode()); }
    catch (NotSupportedException) { threw = true; }
    Assert(threw, "Edge_UnsupportedNodeThrows", cat);
}

{ // Sort with NULLS FIRST
    var sql = emitter.Emit(new SortNode(
        new ScanNode("DeviceProcessEvents"),
        [new SortExpr(new ColumnRef("FileName"), SortDirection.Asc, NullOrder.First)]));
    AssertContains(sql, "NULLS FIRST", "Edge_NullsFirst", cat);
}

{ // Sort with NULLS LAST
    var sql = emitter.Emit(new SortNode(
        new ScanNode("DeviceProcessEvents"),
        [new SortExpr(new ColumnRef("FileName"), SortDirection.Desc, NullOrder.Last)]));
    AssertContains(sql, "NULLS LAST", "Edge_NullsLast", cat);
}

{ // Multi-column sort with KQL default NULLS (spec §11.3: asc→NULLS FIRST, desc→NULLS LAST)
    var sql = emitter.Emit(new SortNode(
        new ScanNode("DeviceProcessEvents"),
        [new SortExpr(new ColumnRef("DeviceName"), SortDirection.Asc),
         new SortExpr(new ColumnRef("Timestamp"), SortDirection.Desc)]));
    AssertContains(sql, "ORDER BY DeviceName ASC NULLS FIRST, Timestamp DESC NULLS LAST", "Edge_MultiSort", cat);
}

{ // CASE expression
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("sev", new CaseScalar(
            [(new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Gt, new LiteralScalar(1000, LiteralKind.Int)),
              new LiteralScalar("high", LiteralKind.String))],
            new LiteralScalar("low", LiteralKind.String)))]));
    AssertContains(sql, "CASE WHEN", "Edge_CaseWhen", cat);
    AssertContains(sql, "ELSE 'low' END", "Edge_CaseElse", cat);
}

{ // Nested functions
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("x", new FunctionCall("tolower",
            [new FunctionCall("substring",
                [new ColumnRef("FileName"), new LiteralScalar(0, LiteralKind.Int), new LiteralScalar(5, LiteralKind.Int)])]))]));
    AssertContains(sql, "lower(substring(FileName", "Edge_NestedFn", cat);
}

{ // NOT expression
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new UnaryScalar(ScalarUnaryOp.Not,
            new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.Eq, new LiteralScalar("cmd.exe", LiteralKind.String)))));
    AssertContains(sql, "NOT", "Edge_UnaryNot", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 12: Schema Emitter
// ════════════════════════════════════════════════════════════════════

cat = "SchemaEmitter";
var schemaEmitter = new SchemaEmitter();

{ // DDL count
    var ddl = schemaEmitter.EmitAll(
        [DeviceProcessEventsSchema.RawWindowsEventJson], [],
        [DeviceProcessEventsSchema.SysmonProcessCreate],
        [DeviceProcessEventsSchema.View]);
    AssertEq(5, ddl.Count, "DDL_Count", cat); // 2 schemas + 1 table + 1 parser view + 1 canonical view
}

{ // Raw table DDL
    var sql = schemaEmitter.EmitCreateTable(DeviceProcessEventsSchema.RawWindowsEventJson);
    AssertContains(sql, "raw.windows_event_json", "RawTable_Name", cat);
    AssertContains(sql, "ingest_time", "RawTable_Column", cat);
    AssertContains(sql, "TIMESTAMP", "RawTable_Type", cat);
    AssertContains(sql, "JSON", "RawTable_JsonType", cat);
}

{ // Parser view DDL
    var sql = schemaEmitter.EmitParserView(DeviceProcessEventsSchema.SysmonProcessCreate);
    AssertContains(sql, "internal.v_process_sysmon_create", "ParserView_Name", cat);
    AssertContains(sql, "raw.windows_event_json", "ParserView_Source", cat);
    AssertContains(sql, "Microsoft-Windows-Sysmon", "ParserView_Filter", cat);
    AssertContains(sql, "json_extract_string", "ParserView_JsonExtract", cat);
    AssertContains(sql, "regexp_extract", "ParserView_RegexExtract", cat);
    // Bug fix: NULL should be typed
    AssertContains(sql, "CAST(NULL AS VARCHAR)", "ParserView_TypedNull", cat);
}

{ // Canonical view DDL
    var sql = schemaEmitter.EmitCanonicalView(DeviceProcessEventsSchema.View);
    AssertContains(sql, "main.DeviceProcessEvents", "CanonicalView_Name", cat);
    AssertContains(sql, "internal.v_process_sysmon_create", "CanonicalView_UnionSource", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 13: SQL Injection Safety
// ════════════════════════════════════════════════════════════════════

cat = "Emitter.Injection";

{ // Quote escaping
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.Eq,
            new LiteralScalar("'; DROP TABLE x; --", LiteralKind.String))));
    AssertContains(sql, "''", "Inject_QuoteEscaped", cat);
    Assert(!Norm(sql).EndsWith("--"), "Inject_NoTrailingComment", cat, Norm(sql));
}

{ // Semicolon
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.Eq,
            new LiteralScalar("test; SELECT 1", LiteralKind.String))));
    Assert(!sql.TrimEnd().EndsWith(";"), "Inject_NoTrailingSemicolon", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 14: String Function Mappings (checklist §4.1)
// ════════════════════════════════════════════════════════════════════

cat = "Func.String";

void TestFn(string kustoName, string expectedSql, ScalarExpr[] args, string testName)
{
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall(kustoName, args))]));
    AssertContains(sql, expectedSql, testName, cat);
}

TestFn("tolower", "lower(FileName)", [new ColumnRef("FileName")], "Fn_tolower");
TestFn("toupper", "upper(FileName)", [new ColumnRef("FileName")], "Fn_toupper");
TestFn("strlen", "length(FileName)", [new ColumnRef("FileName")], "Fn_strlen");
TestFn("strcat", "concat(FileName, DeviceName)",
    [new ColumnRef("FileName"), new ColumnRef("DeviceName")], "Fn_strcat");
TestFn("strcat_delim", "concat_ws",
    [new LiteralScalar(",", LiteralKind.String), new ColumnRef("FileName"), new ColumnRef("DeviceName")], "Fn_strcat_delim");
TestFn("substring", "substring(FileName, (0) + 1, 5)",
    [new ColumnRef("FileName"), new LiteralScalar(0, LiteralKind.Int), new LiteralScalar(5, LiteralKind.Int)], "Fn_substring");
TestFn("replace_string", "replace(FileName,",
    [new ColumnRef("FileName"), new LiteralScalar("old", LiteralKind.String), new LiteralScalar("new", LiteralKind.String)], "Fn_replace_string");
TestFn("replace_regex", "regexp_replace(FileName,",
    [new ColumnRef("FileName"), new LiteralScalar("\\d+", LiteralKind.String), new LiteralScalar("X", LiteralKind.String)], "Fn_replace_regex");

{ // replace_regex must include 'g' flag for global replacement
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("replace_regex",
            [new ColumnRef("FileName"), new LiteralScalar("\\d+", LiteralKind.String), new LiteralScalar("X", LiteralKind.String)]))]));
    AssertContains(sql, "'g'", "Fn_replace_regex_global_flag", cat);
}

TestFn("split", "string_split(FileName,",
    [new ColumnRef("FileName"), new LiteralScalar(",", LiteralKind.String)], "Fn_split");

{ // trim — verify argument order: trim(regex, source) → args[0]=regex, args[1]=source
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("trim",
            [new LiteralScalar("\\s", LiteralKind.String), new ColumnRef("FileName")]))]));
    // The source (FileName) should be the first arg to regexp_replace, not the regex
    AssertContains(sql, "regexp_replace(regexp_replace(FileName", "Fn_trim_arg_order", cat);
}

{ // extract — must wrap with COALESCE and reorder args
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("extract",
            [new LiteralScalar("(\\w+)", LiteralKind.String),
             new LiteralScalar(1, LiteralKind.Int),
             new ColumnRef("ProcessCommandLine")]))]));
    AssertContains(sql, "COALESCE(regexp_extract(ProcessCommandLine", "Fn_extract_coalesce", cat);
    AssertContains(sql, ", '')", "Fn_extract_empty_string_fallback", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 15: Conditional Functions (checklist §3.7)
// ════════════════════════════════════════════════════════════════════

cat = "Func.Conditional";

{ // iff
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("iff",
            [new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Gt, new LiteralScalar(0, LiteralKind.Int)),
             new LiteralScalar("yes", LiteralKind.String),
             new LiteralScalar("no", LiteralKind.String)]))]));
    AssertContains(sql, "CASE WHEN", "Fn_iff_case", cat);
    AssertContains(sql, "THEN 'yes'", "Fn_iff_then", cat);
    AssertContains(sql, "ELSE 'no'", "Fn_iff_else", cat);
}

{ // iif (alias for iff)
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("iif",
            [new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Gt, new LiteralScalar(0, LiteralKind.Int)),
             new LiteralScalar("yes", LiteralKind.String),
             new LiteralScalar("no", LiteralKind.String)]))]));
    AssertContains(sql, "CASE WHEN", "Fn_iif_same_as_iff", cat);
}

{ // case (multi-branch)
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("sev", new CaseScalar(
            [(new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Gt, new LiteralScalar(9000, LiteralKind.Int)),
              new LiteralScalar("critical", LiteralKind.String)),
             (new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Gt, new LiteralScalar(5000, LiteralKind.Int)),
              new LiteralScalar("high", LiteralKind.String)),
             (new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Gt, new LiteralScalar(1000, LiteralKind.Int)),
              new LiteralScalar("medium", LiteralKind.String))],
            new LiteralScalar("low", LiteralKind.String)))]));
    AssertContains(sql, "CASE WHEN", "Fn_case_multi", cat);
    AssertContains(sql, "THEN 'critical'", "Fn_case_branch1", cat);
    AssertContains(sql, "THEN 'high'", "Fn_case_branch2", cat);
    AssertContains(sql, "THEN 'medium'", "Fn_case_branch3", cat);
    AssertContains(sql, "ELSE 'low' END", "Fn_case_default", cat);
}

{ // coalesce
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("coalesce",
            [new ColumnRef("DeviceId"), new ColumnRef("DeviceName"), new LiteralScalar("unknown", LiteralKind.String)]))]));
    AssertContains(sql, "COALESCE(DeviceId, DeviceName, 'unknown')", "Fn_coalesce_3args", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 16: Null Test Functions (checklist §3.8)
// ════════════════════════════════════════════════════════════════════

cat = "Func.NullTests";

{ // isnull
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new FunctionCall("isnull", [new ColumnRef("SHA256")])));
    AssertContains(sql, "SHA256 IS NULL", "Fn_isnull", cat);
}

{ // isnotnull
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new FunctionCall("isnotnull", [new ColumnRef("SHA256")])));
    AssertContains(sql, "SHA256 IS NOT NULL", "Fn_isnotnull", cat);
}

{ // isempty — must check both null AND empty string
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new FunctionCall("isempty", [new ColumnRef("FileName")])));
    AssertContains(sql, "IS NULL OR", "Fn_isempty_null", cat);
    AssertContains(sql, "= ''", "Fn_isempty_empty", cat);
}

{ // isnotempty — must check NOT null AND NOT empty
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new FunctionCall("isnotempty", [new ColumnRef("FileName")])));
    AssertContains(sql, "IS NOT NULL AND", "Fn_isnotempty_notnull", cat);
    AssertContains(sql, "!= ''", "Fn_isnotempty_notempty", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 17: Type Conversion Functions (checklist §4.4)
// ════════════════════════════════════════════════════════════════════

cat = "Func.TypeConversion";

void TestCast(string kustoFn, string expectedType, string testName)
{
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall(kustoFn, [new ColumnRef("ProcessId")]))]));
    AssertContains(sql, $"CAST(ProcessId AS {expectedType})", testName, cat);
}

TestCast("tostring", "VARCHAR", "Fn_tostring");
TestCast("tolong", "BIGINT", "Fn_tolong");
TestCast("toint", "INTEGER", "Fn_toint");
TestCast("todouble", "DOUBLE", "Fn_todouble");
TestCast("tobool", "BOOLEAN", "Fn_tobool");

{ // toreal is alias for todouble
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("toreal", [new ColumnRef("ProcessId")]))]));
    AssertContains(sql, "CAST(ProcessId AS DOUBLE)", "Fn_toreal_alias", cat);
}

{ // todatetime
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("todatetime", [new ColumnRef("FileName")]))]));
    AssertContains(sql, "CAST(FileName AS TIMESTAMP)", "Fn_todatetime", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 18: DateTime Functions (checklist §4.2)
// ════════════════════════════════════════════════════════════════════

cat = "Func.DateTime";

{ // now()
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("now", []))]));
    AssertContains(sql, "current_timestamp", "Fn_now", cat);
}

{ // datetime_part
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("datetime_part",
            [new LiteralScalar("hour", LiteralKind.String), new ColumnRef("Timestamp")]))]));
    AssertContains(sql, "date_part('hour', Timestamp)", "Fn_datetime_part", cat);
}

{ // dayofweek
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("dayofweek", [new ColumnRef("Timestamp")]))]));
    AssertContains(sql, "date_part('dow', Timestamp)", "Fn_dayofweek", cat);
}

{ // dayofmonth
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("dayofmonth", [new ColumnRef("Timestamp")]))]));
    AssertContains(sql, "date_part('day', Timestamp)", "Fn_dayofmonth", cat);
}

{ // dayofyear
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("dayofyear", [new ColumnRef("Timestamp")]))]));
    AssertContains(sql, "date_part('doy', Timestamp)", "Fn_dayofyear", cat);
}

{ // monthofyear
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("monthofyear", [new ColumnRef("Timestamp")]))]));
    AssertContains(sql, "date_part('month', Timestamp)", "Fn_monthofyear", cat);
}

{ // getmonth (alias)
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("getmonth", [new ColumnRef("Timestamp")]))]));
    AssertContains(sql, "date_part('month', Timestamp)", "Fn_getmonth", cat);
}

{ // getyear (alias)
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("getyear", [new ColumnRef("Timestamp")]))]));
    AssertContains(sql, "date_part('year', Timestamp)", "Fn_getyear", cat);
}

{ // hourofday
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("hourofday", [new ColumnRef("Timestamp")]))]));
    AssertContains(sql, "date_part('hour', Timestamp)", "Fn_hourofday", cat);
}

{ // make_datetime
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("make_datetime",
            [new LiteralScalar(2024, LiteralKind.Int), new LiteralScalar(3, LiteralKind.Int),
             new LiteralScalar(15, LiteralKind.Int), new LiteralScalar(14, LiteralKind.Int),
             new LiteralScalar(30, LiteralKind.Int), new LiteralScalar(0, LiteralKind.Int)]))]));
    AssertContains(sql, "make_timestamp(2024, 3, 15, 14, 30, 0)", "Fn_make_datetime", cat);
}

{ // datetime_diff — spec §9.9: KQL datetime_diff(part, dt1, dt2) → DuckDB date_diff(part, dt2, dt1)
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("datetime_diff",
            [new LiteralScalar("second", LiteralKind.String), new ColumnRef("Timestamp"), new ColumnRef("Timestamp")]))]));
    // DuckDB arg order is reversed: date_diff(part, dt2, dt1)
    // So args[1]=dt1 goes to position 3, args[2]=dt2 goes to position 2
    AssertContains(sql, "date_diff('second', Timestamp, Timestamp)", "Fn_datetime_diff", cat);
}

{ // datetime_add
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("datetime_add",
            [new LiteralScalar("hour", LiteralKind.String), new LiteralScalar(3, LiteralKind.Int), new ColumnRef("Timestamp")]))]));
    AssertContains(sql, "INTERVAL '1 hours'", "Fn_datetime_add_interval", cat);
    AssertContains(sql, "Timestamp", "Fn_datetime_add_base", cat);
}

{ // unixtime_seconds_todatetime
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("unixtime_seconds_todatetime", [new ColumnRef("ProcessId")]))]));
    AssertContains(sql, "to_timestamp(ProcessId)", "Fn_unixtime_seconds", cat);
}

{ // unixtime_milliseconds_todatetime
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("unixtime_milliseconds_todatetime", [new ColumnRef("ProcessId")]))]));
    AssertContains(sql, "epoch_ms(CAST(ProcessId AS BIGINT))", "Fn_unixtime_ms", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 19: Aggregation Functions (checklist §4.3)
// ════════════════════════════════════════════════════════════════════

cat = "Func.Aggregation";

void TestAgg(string kustoFn, ScalarExpr[] args, string expectedSql, string testName)
{
    var sql = emitter.Emit(new AggregateNode(
        new ScanNode("DeviceProcessEvents"),
        Aggregates: [new ProjectionExpr("r", new FunctionCall(kustoFn, args))],
        GroupBy: [new ColumnRef("DeviceName")]));
    AssertContains(sql, expectedSql, testName, cat);
}

TestAgg("count", [], "count(*)", "Agg_count");
TestAgg("countif", [new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Gt, new LiteralScalar(0, LiteralKind.Int))],
    "count(*) FILTER (WHERE", "Agg_countif");
TestAgg("sum", [new ColumnRef("ProcessId")], "sum(ProcessId)", "Agg_sum");
TestAgg("sumif", [new ColumnRef("ProcessId"), new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Gt, new LiteralScalar(0, LiteralKind.Int))],
    "sum(ProcessId) FILTER (WHERE", "Agg_sumif");
TestAgg("avg", [new ColumnRef("ProcessId")], "avg(ProcessId)", "Agg_avg");
TestAgg("avgif", [new ColumnRef("ProcessId"), new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Gt, new LiteralScalar(0, LiteralKind.Int))],
    "avg(ProcessId) FILTER (WHERE", "Agg_avgif");
TestAgg("min", [new ColumnRef("Timestamp")], "min(Timestamp)", "Agg_min");
TestAgg("max", [new ColumnRef("Timestamp")], "max(Timestamp)", "Agg_max");
TestAgg("dcount", [new ColumnRef("DeviceName")], "count(DISTINCT DeviceName)", "Agg_dcount");
TestAgg("dcountif", [new ColumnRef("DeviceName"), new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Gt, new LiteralScalar(0, LiteralKind.Int))],
    "count(DISTINCT DeviceName) FILTER (WHERE", "Agg_dcountif");
TestAgg("arg_min", [new ColumnRef("Timestamp"), new ColumnRef("FileName")], "arg_min(Timestamp, FileName)", "Agg_arg_min");
TestAgg("arg_max", [new ColumnRef("Timestamp"), new ColumnRef("FileName")], "arg_max(Timestamp, FileName)", "Agg_arg_max");
TestAgg("make_set", [new ColumnRef("FileName")], "list(DISTINCT FileName)", "Agg_make_set");
TestAgg("make_list", [new ColumnRef("FileName")], "list(FileName)", "Agg_make_list");

{ // make_set with limit
    var sql = emitter.Emit(new AggregateNode(
        new ScanNode("DeviceProcessEvents"),
        Aggregates: [new ProjectionExpr("r", new FunctionCall("make_set",
            [new ColumnRef("FileName"), new LiteralScalar(10, LiteralKind.Int)]))],
        GroupBy: [new ColumnRef("DeviceName")]));
    AssertContains(sql, "list_slice(list(DISTINCT FileName), 1, 10)", "Agg_make_set_limited", cat);
}

{ // make_list with limit
    var sql = emitter.Emit(new AggregateNode(
        new ScanNode("DeviceProcessEvents"),
        Aggregates: [new ProjectionExpr("r", new FunctionCall("make_list",
            [new ColumnRef("FileName"), new LiteralScalar(5, LiteralKind.Int)]))],
        GroupBy: [new ColumnRef("DeviceName")]));
    AssertContains(sql, "list_slice(list(FileName), 1, 5)", "Agg_make_list_limited", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 20: JSON / Dynamic Functions (checklist §4.5)
// ════════════════════════════════════════════════════════════════════

cat = "Func.JSON";

{ // parse_json
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("parse_json", [new ColumnRef("AdditionalFields")]))]));
    AssertContains(sql, "CAST(AdditionalFields AS JSON)", "Fn_parse_json", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 21: Math Functions (checklist §4.6)
// ════════════════════════════════════════════════════════════════════

cat = "Func.Math";

{ // abs
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("abs", [new ColumnRef("ProcessId")]))]));
    AssertContains(sql, "abs(ProcessId)", "Fn_abs", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 22: String Operators — case-sensitive variants (§3.5)
// ════════════════════════════════════════════════════════════════════

cat = "Ops.StringCs";

void TestStringOp(ScalarBinaryOp op, string expectedPattern, string testName, bool expectLike, bool expectILike)
{
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("FileName"), op, new LiteralScalar("test", LiteralKind.String))));
    var norm = Norm(sql);
    if (expectILike) Assert(norm.Contains("ILIKE"), testName + "_ILIKE", cat, norm);
    if (expectLike) Assert(norm.Contains(" LIKE ") && !norm.Contains("ILIKE"), testName + "_LIKE_notILIKE", cat, norm);
    AssertContains(sql, expectedPattern, testName, cat);
}

TestStringOp(ScalarBinaryOp.ContainsCs, "'%' || 'test' || '%'", "Op_contains_cs", expectLike: true, expectILike: false);
TestStringOp(ScalarBinaryOp.NotContainsCs, "NOT LIKE", "Op_not_contains_cs", expectLike: true, expectILike: false);
TestStringOp(ScalarBinaryOp.StartsWithCs, "'test' || '%'", "Op_startswith_cs", expectLike: true, expectILike: false);
TestStringOp(ScalarBinaryOp.NotStartsWithCs, "NOT LIKE", "Op_not_startswith_cs", expectLike: true, expectILike: false);
TestStringOp(ScalarBinaryOp.EndsWithCs, "'%' || 'test'", "Op_endswith_cs", expectLike: true, expectILike: false);
TestStringOp(ScalarBinaryOp.NotEndsWithCs, "NOT LIKE", "Op_not_endswith_cs", expectLike: true, expectILike: false);

// Verify case-insensitive variants use ILIKE (not LIKE)
TestStringOp(ScalarBinaryOp.Contains, "ILIKE", "Op_contains_ci", expectLike: false, expectILike: true);
TestStringOp(ScalarBinaryOp.StartsWith, "ILIKE", "Op_startswith_ci", expectLike: false, expectILike: true);
TestStringOp(ScalarBinaryOp.EndsWith, "ILIKE", "Op_endswith_ci", expectLike: false, expectILike: true);

// ════════════════════════════════════════════════════════════════════
// CATEGORY 23: Comparison Operators (checklist §3.3)
// ════════════════════════════════════════════════════════════════════

cat = "Ops.Comparison";

void TestBinOp(ScalarBinaryOp op, string expectedSql, string testName)
{
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("ProcessId"), op, new LiteralScalar(100, LiteralKind.Int))));
    AssertContains(sql, expectedSql, testName, cat);
}

TestBinOp(ScalarBinaryOp.Eq, "ProcessId = 100", "Op_eq");
TestBinOp(ScalarBinaryOp.Neq, "ProcessId != 100", "Op_neq");
TestBinOp(ScalarBinaryOp.Lt, "ProcessId < 100", "Op_lt");
TestBinOp(ScalarBinaryOp.Lte, "ProcessId <= 100", "Op_lte");
TestBinOp(ScalarBinaryOp.Gt, "ProcessId > 100", "Op_gt");
TestBinOp(ScalarBinaryOp.Gte, "ProcessId >= 100", "Op_gte");

{ // IN operator
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.In,
            new LiteralScalar("('cmd.exe','powershell.exe')", LiteralKind.String))));
    AssertContains(sql, "IN", "Op_in", cat);
}

{ // NOT IN operator
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.NotIn,
            new LiteralScalar("('cmd.exe','powershell.exe')", LiteralKind.String))));
    AssertContains(sql, "NOT IN", "Op_not_in", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 24: Arithmetic Operators (checklist §3.2)
// ════════════════════════════════════════════════════════════════════

cat = "Ops.Arithmetic";

void TestArith(ScalarBinaryOp op, string expectedOp, string testName)
{
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new BinaryScalar(new ColumnRef("ProcessId"), op, new LiteralScalar(10, LiteralKind.Int)))]));
    AssertContains(sql, expectedOp, testName, cat);
}

TestArith(ScalarBinaryOp.Add, "ProcessId + 10", "Arith_add");
TestArith(ScalarBinaryOp.Sub, "ProcessId - 10", "Arith_sub");
TestArith(ScalarBinaryOp.Mul, "ProcessId * 10", "Arith_mul");
TestArith(ScalarBinaryOp.Div, "ProcessId / 10", "Arith_div");
TestArith(ScalarBinaryOp.Mod, "ProcessId % 10", "Arith_mod");

{ // Unary negation
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new UnaryScalar(ScalarUnaryOp.Negate, new ColumnRef("ProcessId")))]));
    AssertContains(sql, "(-ProcessId)", "Arith_negate", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 25: Logical Operators (checklist §3.4)
// ════════════════════════════════════════════════════════════════════

cat = "Ops.Logical";

{ // AND
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(
            new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Gt, new LiteralScalar(0, LiteralKind.Int)),
            ScalarBinaryOp.And,
            new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Lt, new LiteralScalar(1000, LiteralKind.Int)))));
    AssertContains(sql, "AND", "Logic_and", cat);
}

{ // OR
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(
            new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.Eq, new LiteralScalar("cmd.exe", LiteralKind.String)),
            ScalarBinaryOp.Or,
            new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.Eq, new LiteralScalar("powershell.exe", LiteralKind.String)))));
    AssertContains(sql, "OR", "Logic_or", cat);
}

{ // NOT
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new UnaryScalar(ScalarUnaryOp.Not,
            new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Eq, new LiteralScalar(0, LiteralKind.Int)))));
    AssertContains(sql, "NOT", "Logic_not", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 26: DateTime Truncation Functions (§4.2 startof/endof)
// ════════════════════════════════════════════════════════════════════

cat = "Func.DateTrunc";

void TestDateFn(string kustoFn, string expectedSql, string testName)
{
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall(kustoFn, [new ColumnRef("Timestamp")]))]));
    AssertContains(sql, expectedSql, testName, cat);
}

TestDateFn("startofday", "date_trunc('day', Timestamp)", "DT_startofday");
TestDateFn("startofmonth", "date_trunc('month', Timestamp)", "DT_startofmonth");
TestDateFn("startofweek", "date_trunc('week', Timestamp)", "DT_startofweek");
TestDateFn("startofyear", "date_trunc('year', Timestamp)", "DT_startofyear");
TestDateFn("endofday", "date_trunc('day', Timestamp)", "DT_endofday_trunc"); // contains trunc as part of the formula
TestDateFn("endofmonth", "last_day(Timestamp)", "DT_endofmonth_lastday");
TestDateFn("endofweek", "date_trunc('week', Timestamp)", "DT_endofweek_trunc");
TestDateFn("endofyear", "date_trunc('year', Timestamp)", "DT_endofyear_trunc");

{ // endofday must subtract 1 microsecond
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("endofday", [new ColumnRef("Timestamp")]))]));
    AssertContains(sql, "1 microsecond", "DT_endofday_microsecond", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 27: Epoch Functions (§4.2)
// ════════════════════════════════════════════════════════════════════

cat = "Func.Epoch";

{ // unixtime_microseconds_todatetime
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("unixtime_microseconds_todatetime", [new ColumnRef("ProcessId")]))]));
    AssertContains(sql, "make_timestamp(ProcessId)", "Fn_unixtime_us", cat);
}

{ // unixtime_nanoseconds_todatetime
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", new FunctionCall("unixtime_nanoseconds_todatetime", [new ColumnRef("ProcessId")]))]));
    AssertContains(sql, "make_timestamp_ns(ProcessId)", "Fn_unixtime_ns", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 28: Tabular Operators — count, distinct, top (§1.1)
// ════════════════════════════════════════════════════════════════════

cat = "Ops.Tabular";

{ // count (shorthand for summarize count())
    var node = new AggregateNode(
        new ScanNode("DeviceProcessEvents"),
        Aggregates: [new ProjectionExpr("Count", new FunctionCall("count", []))],
        GroupBy: []);
    var sql = emitter.Emit(node);
    AssertContains(sql, "count(*) AS Count", "Tab_count", cat);
    AssertNotContains(sql, "GROUP BY", "Tab_count_no_groupby", cat);
}

{ // distinct — must emit SELECT DISTINCT, not plain SELECT
    var node = new DistinctNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("FileName", new ColumnRef("FileName")),
         new ProjectionExpr("DeviceName", new ColumnRef("DeviceName"))]);
    var sql = emitter.Emit(node);
    AssertContains(sql, "SELECT DISTINCT FileName, DeviceName FROM", "Tab_distinct_DISTINCT", cat);
    AssertNotContains(sql, "SELECT FileName, DeviceName", "Tab_distinct_not_plain_select", cat);
}

{ // top → sort + limit
    var node = new LimitNode(
        new SortNode(
            new ScanNode("DeviceProcessEvents"),
            [new SortExpr(new ColumnRef("Timestamp"), SortDirection.Desc)]),
        5);
    var sql = emitter.Emit(node);
    AssertContains(sql, "ORDER BY Timestamp DESC", "Tab_top_sort", cat);
    AssertContains(sql, "LIMIT 5", "Tab_top_limit", cat);
}

{ // INNER JOIN
    var node = new JoinNode(
        new ScanNode("DeviceProcessEvents"),
        new ScanNode("DeviceProcessEvents"),
        JoinKind.Inner,
        new BinaryScalar(new ColumnRef("DeviceName"), ScalarBinaryOp.Eq, new ColumnRef("DeviceName")));
    var sql = emitter.Emit(node);
    AssertContains(sql, "INNER JOIN", "Tab_join_inner", cat);
}

{ // LEFT JOIN
    var node = new JoinNode(
        new ScanNode("DeviceProcessEvents"),
        new ScanNode("DeviceProcessEvents"),
        JoinKind.LeftOuter,
        new BinaryScalar(new ColumnRef("DeviceName"), ScalarBinaryOp.Eq, new ColumnRef("DeviceName")));
    var sql = emitter.Emit(node);
    AssertContains(sql, "LEFT JOIN", "Tab_join_leftouter", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 29: Schema Emitter — Mapping Expression Types
// ════════════════════════════════════════════════════════════════════

cat = "SchemaEmitter.Mapping";

{ // JSON extraction in parser view
    var sql = schemaEmitter.EmitParserView(DeviceProcessEventsSchema.SysmonProcessCreate);
    AssertContains(sql, "json_extract_string", "Map_JsonExtract", cat);
    AssertContains(sql, "regexp_extract", "Map_RegexExtract", cat);
    AssertContains(sql, "CAST(", "Map_Cast", cat);
    // Filter condition present
    AssertContains(sql, "WHERE", "Map_WhereClause", cat);
    AssertContains(sql, "AND", "Map_CompoundFilter", cat);
    // Typed NULLs
    AssertContains(sql, "CAST(NULL AS VARCHAR) AS DeviceId", "Map_TypedNull_DeviceId", cat);
    AssertContains(sql, "CAST(NULL AS VARCHAR) AS ReportId", "Map_TypedNull_ReportId", cat);
}

{ // Canonical view with multiple parser views
    var multiView = new CanonicalViewDef(
        Schema: "main", Name: "MultiSource",
        ParserViews: ["internal.v_source_a", "internal.v_source_b", "internal.v_source_c"],
        Columns: [new ColumnDef("Timestamp", DuckDbType.Timestamp, KustoType.DateTime)],
        Description: "Multi-source test");
    var sql = schemaEmitter.EmitCanonicalView(multiView);
    AssertContains(sql, "UNION ALL", "Map_UnionAll", cat);
    AssertContains(sql, "internal.v_source_a", "Map_Source_A", cat);
    AssertContains(sql, "internal.v_source_b", "Map_Source_B", cat);
    AssertContains(sql, "internal.v_source_c", "Map_Source_C", cat);
    // Should have exactly 2 UNION ALL for 3 sources
    var unionCount = Regex.Matches(sql, "UNION ALL").Count;
    AssertEq(2, unionCount, "Map_UnionAll_Count", cat);
}

{ // Canonical view with zero parser views throws
    bool threw = false;
    try
    {
        schemaEmitter.EmitCanonicalView(new CanonicalViewDef(
            Schema: "main", Name: "Empty",
            ParserViews: [],
            Columns: [new ColumnDef("X", DuckDbType.Varchar, KustoType.String)]));
    }
    catch (InvalidOperationException) { threw = true; }
    Assert(threw, "Map_EmptyParserViews_Throws", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 30: Has Operators — word-boundary matching (§3.5)
// ════════════════════════════════════════════════════════════════════

cat = "Ops.Has";

{ // has — regex-escaped literal term, POSIX boundary, case-insensitive via lower()
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("ProcessCommandLine"), ScalarBinaryOp.Has,
            new LiteralScalar("mimikatz", LiteralKind.String))));
    AssertContains(sql, "regexp_matches(lower(ProcessCommandLine)", "Has_uses_lower", cat);
    AssertContains(sql, "[^[:alnum:]]", "Has_posix_boundary", cat);
    // Literal term "mimikatz" has no metacharacters — appears pre-escaped directly
    AssertContains(sql, "'mimikatz'", "Has_literal_term", cat);
    AssertNotContains(sql, "(?i)", "Has_no_i_flag", cat);
    AssertNotContains(sql, "\\b", "Has_no_backslash_b", cat);
}

{ // has with metachar term — c++ should be escaped to c\+\+ in regex
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("ProcessCommandLine"), ScalarBinaryOp.Has,
            new LiteralScalar("c++", LiteralKind.String))));
    // .NET Regex.Escape("c++") = "c\+\+"; the literal should appear escaped
    AssertNotContains(sql, "'c++'", "Has_metachar_unescaped", cat);
    AssertContains(sql, "[^[:alnum:]]", "Has_metachar_boundary", cat);
}

{ // !has — negated
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("ProcessCommandLine"), ScalarBinaryOp.NotHas,
            new LiteralScalar("mimikatz", LiteralKind.String))));
    AssertContains(sql, "NOT regexp_matches(lower(", "NotHas_negation", cat);
    AssertContains(sql, "[^[:alnum:]]", "NotHas_posix_boundary", cat);
}

{ // has_cs — case-sensitive: no lower(), uses 'c' flag
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("ProcessCommandLine"), ScalarBinaryOp.HasCs,
            new LiteralScalar("Mimikatz", LiteralKind.String))));
    // Spec §7.9 cs variant: regexp_matches(lhs, boundary || rhs || boundary, 'c')
    AssertContains(sql, "regexp_matches(ProcessCommandLine", "HasCs_no_lower", cat);
    AssertContains(sql, "'c'", "HasCs_case_flag", cat);
    AssertContains(sql, "[^[:alnum:]]", "HasCs_posix_boundary", cat);
    AssertNotContains(sql, "lower(ProcessCommandLine", "HasCs_not_lowered", cat);
}

{ // !has_cs — negated case-sensitive
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("ProcessCommandLine"), ScalarBinaryOp.NotHasCs,
            new LiteralScalar("Mimikatz", LiteralKind.String))));
    AssertContains(sql, "NOT regexp_matches(ProcessCommandLine", "NotHasCs_negation", cat);
    AssertContains(sql, "'c'", "NotHasCs_case_flag", cat);
}

{ // hasprefix — leading boundary only, case-insensitive via lower()
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.HasPrefix,
            new LiteralScalar("power", LiteralKind.String))));
    AssertContains(sql, "regexp_matches(lower(FileName)", "HasPrefix_lower_lhs", cat);
    AssertContains(sql, "(^|[^[:alnum:]])", "HasPrefix_leading_boundary", cat);
    // hasprefix should NOT have trailing boundary
    AssertNotContains(sql, "([^[:alnum:]]|$)", "HasPrefix_no_trailing", cat);
}

{ // hassuffix — trailing boundary only, case-insensitive via lower()
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.HasSuffix,
            new LiteralScalar("shell", LiteralKind.String))));
    AssertContains(sql, "regexp_matches(lower(FileName)", "HasSuffix_lower_lhs", cat);
    AssertContains(sql, "([^[:alnum:]]|$)", "HasSuffix_trailing_boundary", cat);
    // hassuffix should NOT have leading boundary
    AssertNotContains(sql, "(^|[^[:alnum:]])", "HasSuffix_no_leading", cat);
}

{ // matches regex — must use 'c' case-sensitive flag per spec §7.14
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("ProcessCommandLine"), ScalarBinaryOp.MatchesRegex,
            new LiteralScalar("(?i)pass(word|wd)", LiteralKind.String))));
    AssertContains(sql, "regexp_matches(ProcessCommandLine", "MatchesRegex_fn", cat);
    AssertContains(sql, "'c'", "MatchesRegex_case_flag", cat);
}

{ // !matches regex — negated with 'c' flag
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("ProcessCommandLine"), ScalarBinaryOp.NotMatchesRegex,
            new LiteralScalar("\\d{4}", LiteralKind.String))));
    AssertContains(sql, "NOT regexp_matches(", "NotMatchesRegex_negation", cat);
    AssertContains(sql, "'c'", "NotMatchesRegex_case_flag", cat);
}

{ // has vs contains — different SQL output (has=POSIX boundary, contains=ILIKE)
    var hasSql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.Has,
            new LiteralScalar("cmd", LiteralKind.String))));
    var containsSql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.Contains,
            new LiteralScalar("cmd", LiteralKind.String))));
    Assert(Norm(hasSql).Contains("regexp_matches") && !Norm(hasSql).Contains("ILIKE"),
        "HasVsContains_has_uses_regex", cat, Norm(hasSql));
    Assert(Norm(containsSql).Contains("ILIKE") && !Norm(containsSql).Contains("regexp_matches"),
        "HasVsContains_contains_uses_ilike", cat, Norm(containsSql));
}

{ // has correctness: "amer" is not a term in "North America" (partial word)
    // The POSIX boundary (^|[^[:alnum:]])amer([^[:alnum:]]|$) would NOT match "America"
    // This documents the semantic correctness of the boundary choice
    var sql = emitter.Emit(new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.Has,
            new LiteralScalar("amer", LiteralKind.String))));
    // Pattern must NOT use \b (which would match "amer" inside "america" under some engines)
    AssertNotContains(sql, "\\b", "Has_semantics_noBackslashB", cat);
    // Pattern must use alphanumeric boundary
    AssertContains(sql, "[^[:alnum:]]", "Has_semantics_posixBoundary", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 31: Emitter — Robustness (continued)
// ════════════════════════════════════════════════════════════════════

cat = "Emitter.Robustness";

{ // Unknown function now throws NotSupportedException — safety rule: no silent passthrough
    bool threw = false;
    try
    {
        emitter.Emit(new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("r", new FunctionCall("custom_function_xyz",
                [new ColumnRef("FileName"), new LiteralScalar(42, LiteralKind.Int)]))]));
    }
    catch (NotSupportedException ex) when (ex.Message.Contains("allowlist"))
    {
        threw = true;
    }
    Assert(threw, "Robust_UnknownFn_Rejected", cat,
        "Unknown function should throw NotSupportedException per safety rule — no silent SQL passthrough");
}

{ // Nested CASEs
    var innerCase = new CaseScalar(
        [(new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Gt, new LiteralScalar(100, LiteralKind.Int)),
          new LiteralScalar("inner_yes", LiteralKind.String))],
        new LiteralScalar("inner_no", LiteralKind.String));
    var outerCase = new CaseScalar(
        [(new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Gt, new LiteralScalar(0, LiteralKind.Int)),
          innerCase)],
        new LiteralScalar("outer_no", LiteralKind.String));
    var sql = emitter.Emit(new ExtendNode(
        new ScanNode("DeviceProcessEvents"),
        [new ProjectionExpr("r", outerCase)]));
    AssertContains(sql, "CASE WHEN", "Robust_NestedCase", cat);
    AssertContains(sql, "'inner_yes'", "Robust_NestedCase_InnerBranch", cat);
}

{ // Multiple aggregation functions in single summarize
    var sql = emitter.Emit(new AggregateNode(
        new ScanNode("DeviceProcessEvents"),
        Aggregates: [
            new ProjectionExpr("cnt", new FunctionCall("count", [])),
            new ProjectionExpr("total", new FunctionCall("sum", [new ColumnRef("ProcessId")])),
            new ProjectionExpr("earliest", new FunctionCall("min", [new ColumnRef("Timestamp")])),
            new ProjectionExpr("latest", new FunctionCall("max", [new ColumnRef("Timestamp")])),
            new ProjectionExpr("uniq", new FunctionCall("dcount", [new ColumnRef("FileName")]))],
        GroupBy: [new ColumnRef("DeviceName")]));
    AssertContains(sql, "count(*) AS cnt", "Robust_MultiAgg_count", cat);
    AssertContains(sql, "sum(ProcessId) AS total", "Robust_MultiAgg_sum", cat);
    AssertContains(sql, "min(Timestamp) AS earliest", "Robust_MultiAgg_min", cat);
    AssertContains(sql, "max(Timestamp) AS latest", "Robust_MultiAgg_max", cat);
    AssertContains(sql, "count(DISTINCT FileName) AS uniq", "Robust_MultiAgg_dcount", cat);
    AssertContains(sql, "GROUP BY DeviceName", "Robust_MultiAgg_groupby", cat);
}

{ // Multiple GROUP BY columns
    var sql = emitter.Emit(new AggregateNode(
        new ScanNode("DeviceProcessEvents"),
        Aggregates: [new ProjectionExpr("cnt", new FunctionCall("count", []))],
        GroupBy: [new ColumnRef("DeviceName"), new ColumnRef("FileName"), new ColumnRef("AccountName")]));
    AssertContains(sql, "GROUP BY DeviceName, FileName, AccountName", "Robust_MultiGroupBy", cat);
}

{ // LetBindingNode — tabular let
    var sql = emitter.Emit(new LetBindingNode(
        Name: "filtered",
        TabularValue: new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Gt, new LiteralScalar(0, LiteralKind.Int))),
        ScalarValue: null,
        Body: new LimitNode(new ScanNode("DeviceProcessEvents"), 10)));
    AssertContains(sql, "WITH", "Robust_Let_CTE", cat);
    AssertContains(sql, "filtered", "Robust_Let_Name", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 32: Scalar Let Substitution (P0-2 fix)
// ════════════════════════════════════════════════════════════════════

cat = "Emitter.Let";

{ // Scalar let with timespan: let cutoff = ago(7d); T | where Timestamp > cutoff
    // cutoff must be inlined as the ago expression, not emitted as a bare column ref
    var node = new LetBindingNode(
        Name: "cutoff",
        ScalarValue: new FunctionCall("ago", [new LiteralScalar("7d", LiteralKind.Timespan)]),
        TabularValue: null,
        Body: new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new ColumnRef("Timestamp"),
                ScalarBinaryOp.Gt,
                new ColumnRef("cutoff"))));  // references the let name

    var sql = emitter.Emit(node);
    // cutoff must be substituted — SQL must NOT contain a bare reference to "cutoff"
    AssertNotContains(sql, "cutoff", "Let_Scalar_NoBareName", cat);
    // The inlined value must appear
    AssertContains(sql, "current_timestamp - INTERVAL '7 days'", "Let_Scalar_Inlined", cat);
    AssertContains(sql, "Timestamp >", "Let_Scalar_Predicate", cat);
}

{ // Scalar let with string literal
    var node = new LetBindingNode(
        Name: "target",
        ScalarValue: new LiteralScalar("cmd.exe", LiteralKind.String),
        TabularValue: null,
        Body: new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new ColumnRef("FileName"),
                ScalarBinaryOp.Eq,
                new ColumnRef("target"))));

    var sql = emitter.Emit(node);
    AssertNotContains(sql, " target", "Let_StringLit_NoBareName", cat);
    AssertContains(sql, "'cmd.exe'", "Let_StringLit_Inlined", cat);
}

{ // Scalar let does not pollute next query (cross-query isolation)
    var node1 = new LetBindingNode(
        Name: "magic",
        ScalarValue: new LiteralScalar(42, LiteralKind.Int),
        TabularValue: null,
        Body: new ScanNode("DeviceProcessEvents"));
    emitter.Emit(node1);

    // Second emission: "magic" should be a bare column ref, not substituted
    var node2 = new FilterNode(
        new ScanNode("DeviceProcessEvents"),
        new BinaryScalar(new ColumnRef("magic"), ScalarBinaryOp.Gt, new LiteralScalar(0, LiteralKind.Int)));
    var sql2 = emitter.Emit(node2);
    AssertContains(sql2, "magic", "Let_Isolation_NamePresent", cat);
    AssertNotContains(sql2, "42", "Let_Isolation_ValueAbsent", cat);
}

{ // Tabular let creates a named CTE
    var node = new LetBindingNode(
        Name: "PowerShellProcs",
        TabularValue: new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.Eq,
                new LiteralScalar("powershell.exe", LiteralKind.String))),
        ScalarValue: null,
        Body: new ScanNode("DeviceProcessEvents"));

    var sql = emitter.Emit(node);
    AssertContains(sql, "WITH", "Let_Tabular_CTE", cat);
    AssertContains(sql, "PowerShellProcs", "Let_Tabular_Name", cat);
    AssertContains(sql, "powershell.exe", "Let_Tabular_Filter", cat);
}

// ════════════════════════════════════════════════════════════════════
// CATEGORY 33: Join On-Column Semantics (P0-5 fix)
// ════════════════════════════════════════════════════════════════════

cat = "Emitter.JoinOn";

{ // INNER JOIN ON equality predicate emits valid SQL
    var node = new JoinNode(
        new ScanNode("DeviceProcessEvents"),
        new ScanNode("DeviceProcessEvents"),
        JoinKind.Inner,
        new BinaryScalar(
            new ColumnRef("DeviceName"),
            ScalarBinaryOp.Eq,
            new ColumnRef("DeviceName")));

    var sql = emitter.Emit(node);
    AssertContains(sql, "INNER JOIN", "JoinOn_Inner", cat);
    // Must emit ON ... = ... not bare ON DeviceName
    AssertContains(sql, "ON (DeviceName = DeviceName)", "JoinOn_EqualityPredicate", cat);
}

{ // LEFT JOIN with multi-column equality AND chain
    var node = new JoinNode(
        new ScanNode("DeviceProcessEvents"),
        new ScanNode("DeviceProcessEvents"),
        JoinKind.LeftOuter,
        new BinaryScalar(
            new BinaryScalar(new ColumnRef("DeviceName"), ScalarBinaryOp.Eq, new ColumnRef("DeviceName")),
            ScalarBinaryOp.And,
            new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Eq, new ColumnRef("ProcessId"))));

    var sql = emitter.Emit(node);
    AssertContains(sql, "LEFT JOIN", "JoinOn_Left", cat);
    AssertContains(sql, "DeviceName = DeviceName", "JoinOn_Multi_Col1", cat);
    AssertContains(sql, "ProcessId = ProcessId", "JoinOn_Multi_Col2", cat);
    AssertContains(sql, "AND", "JoinOn_Multi_And", cat);
}

{ // SEMI JOIN
    var node = new JoinNode(
        new ScanNode("DeviceProcessEvents"),
        new ScanNode("DeviceProcessEvents"),
        JoinKind.LeftSemi,
        new BinaryScalar(new ColumnRef("DeviceName"), ScalarBinaryOp.Eq, new ColumnRef("DeviceName")));
    var sql = emitter.Emit(node);
    AssertContains(sql, "SEMI JOIN", "JoinOn_Semi", cat);
}

{ // ANTI JOIN
    var node = new JoinNode(
        new ScanNode("DeviceProcessEvents"),
        new ScanNode("DeviceProcessEvents"),
        JoinKind.LeftAnti,
        new BinaryScalar(new ColumnRef("DeviceName"), ScalarBinaryOp.Eq, new ColumnRef("DeviceName")));
    var sql = emitter.Emit(node);
    AssertContains(sql, "ANTI JOIN", "JoinOn_Anti", cat);
}

{ // HasLimit on join — join output is unbounded; must always apply safety cap
    var joinNode = new JoinNode(
        new ScanNode("DeviceProcessEvents"),
        new LimitNode(new ScanNode("DeviceProcessEvents"), 5), // limit on RIGHT branch only
        JoinKind.Inner,
        new BinaryScalar(new ColumnRef("DeviceName"), ScalarBinaryOp.Eq, new ColumnRef("DeviceName")));

    var e2 = new DuckDbQueryEmitter(defaultLimit: 100);
    var sql = e2.Emit(joinNode);
    // The safety cap must still be applied — right-branch limit does not bound join output
    AssertContains(sql, "LIMIT 100", "JoinOn_SafetyCap", cat);
}

Console.WriteLine($"\n{"═",-60}");
Console.WriteLine($"  RESULTS: {passed} passed, {failed} failed, {total} total");
Console.WriteLine($"{"═",-60}\n");

if (failures.Count > 0)
{
    // Group by category
    var grouped = failures.GroupBy(f => f.Category).OrderBy(g => g.Key);
    foreach (var group in grouped)
    {
        Console.WriteLine($"  [{group.Key}] — {group.Count()} failure(s)");
        foreach (var (_, test, error) in group)
        {
            Console.WriteLine($"    FAIL: {test}");
            if (!string.IsNullOrEmpty(error))
                Console.WriteLine($"          {error}");
        }
        Console.WriteLine();
    }
}

return failed > 0 ? 1 : 0;

// Dummy type for unsupported node test
record UnsupportedTestNode() : RelNode;
