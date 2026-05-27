namespace Hunting.Tests.Emitter;

using System.Text.RegularExpressions;
using Hunting.Core.DuckDbSql;
using Hunting.Core.QueryModel;

/// <summary>
/// Edge case, boundary, and adversarial tests for the DuckDB query emitter.
/// These verify that the emitter handles malformed, extreme, or adversarial
/// RelNode trees without producing broken SQL or exposing internal state.
/// </summary>
[TestClass]
public sealed partial class DuckDbQueryEmitterEdgeCaseTests
{
    private readonly DuckDbQueryEmitter _emitter = new(defaultLimit: 10_000);

    // ─── Null and empty values ──────────────────────────────────────

    [TestMethod]
    [Description("NULL literal emits SQL NULL")]
    public void Literal_Null()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("empty_col",
                new LiteralScalar(null, LiteralKind.Null))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "NULL AS empty_col");
    }

    [TestMethod]
    [Description("Empty string literal emits ''")]
    public void Literal_EmptyString()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new ColumnRef("FileName"),
                ScalarBinaryOp.Eq,
                new LiteralScalar("", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "= ''");
    }

    [TestMethod]
    [Description("Boolean true literal emits TRUE")]
    public void Literal_BoolTrue()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("flag",
                new LiteralScalar(true, LiteralKind.Bool))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "TRUE AS flag");
    }

    [TestMethod]
    [Description("Boolean false literal emits FALSE")]
    public void Literal_BoolFalse()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("flag",
                new LiteralScalar(false, LiteralKind.Bool))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "FALSE AS flag");
    }

    // ─── SQL injection via string literals ───────────────────────────

    [TestMethod]
    [Description("Single quote in string literal is escaped")]
    public void Injection_SingleQuote()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new ColumnRef("FileName"),
                ScalarBinaryOp.Eq,
                new LiteralScalar("it's", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "'it''s'");
        // Must NOT contain unescaped single quote that would break SQL
        Assert.DoesNotContain("'it's'", NormSql(sql),
            "Unescaped single quote would produce SQL injection");
    }

    [TestMethod]
    [Description("SQL injection attempt via malicious string literal")]
    public void Injection_DropTable()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new ColumnRef("FileName"),
                ScalarBinaryOp.Eq,
                new LiteralScalar("'; DROP TABLE main.DeviceProcessEvents; --", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        // The leading single quote in the payload must be escaped to ''
        AssertSqlContains(sql, "''");
        // The SQL must be a single statement — no unquoted semicolons.
        // The emitter never appends semicolons, so the output must not end
        // with executable SQL after the string literal closes.
        // Count quote characters: the escaped payload should be fully enclosed.
        // If escaping is correct, the SQL parser sees one string literal
        // containing the injection text, not a statement boundary.
        var norm = NormSql(sql);
        // The SQL should NOT end with "--" (which would mean the comment
        // delimiter leaked out of the string literal)
        Assert.DoesNotEndWith("--", norm,
            "SQL comment delimiter should be inside the string literal, not at statement end");
    }

    [TestMethod]
    [Description("Semicolon in string literal does not create second statement")]
    public void Injection_Semicolon()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new ColumnRef("FileName"),
                ScalarBinaryOp.Eq,
                new LiteralScalar("test; SELECT * FROM internal.secrets", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        // Entire payload must be within quotes
        Assert.DoesNotEndWith(";", sql.TrimEnd(),
            "Emitted SQL should not end with semicolon from injected payload");
    }

    [TestMethod]
    [Description("Backslash in string literal preserved")]
    public void Literal_Backslash()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new ColumnRef("FolderPath"),
                ScalarBinaryOp.Eq,
                new LiteralScalar(@"C:\Windows\System32\cmd.exe", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, @"C:\Windows\System32\cmd.exe");
    }

    [TestMethod]
    [Description("Unicode in string literal preserved")]
    public void Literal_Unicode()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new ColumnRef("AccountName"),
                ScalarBinaryOp.Eq,
                new LiteralScalar("müller@contoso.com", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "müller@contoso.com");
    }

    // ─── Boundary limits ────────────────────────────────────────────

    [TestMethod]
    [Description("Limit 0 emits LIMIT 0")]
    public void Limit_Zero()
    {
        var node = new LimitNode(new ScanNode("DeviceProcessEvents"), 0);
        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "LIMIT 0");
    }

    [TestMethod]
    [Description("Limit 1 emits LIMIT 1")]
    public void Limit_One()
    {
        var node = new LimitNode(new ScanNode("DeviceProcessEvents"), 1);
        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "LIMIT 1");
    }

    [TestMethod]
    [Description("Very large limit value")]
    public void Limit_VeryLarge()
    {
        var node = new LimitNode(new ScanNode("DeviceProcessEvents"), 1_000_000);
        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "LIMIT 1000000");
    }

    [TestMethod]
    [Description("Default safety limit injected when no user LIMIT")]
    public void Limit_DefaultInjected()
    {
        var node = new ScanNode("DeviceProcessEvents");
        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "LIMIT 10000");
    }

    [TestMethod]
    [Description("Default limit NOT injected when user provides LIMIT")]
    public void Limit_NotDoubled()
    {
        var node = new LimitNode(new ScanNode("DeviceProcessEvents"), 50);
        var sql = _emitter.Emit(node);
        var norm = NormSql(sql);
        // Count occurrences of LIMIT — should be exactly one
        var limitCount = LimitPattern().Count(norm);
        Assert.AreEqual(1, limitCount, $"Expected exactly one LIMIT clause, got {limitCount} in: {norm}");
    }

    // ─── Deep pipeline nesting ──────────────────────────────────────

    [TestMethod]
    [Description("10-stage pipeline produces valid CTE chain")]
    public void Deep_Pipeline_10Stages()
    {
        RelNode node = new ScanNode("DeviceProcessEvents");
        for (var i = 0; i < 5; i++)
        {
            node = new FilterNode(node, new BinaryScalar(
                new ColumnRef("ProcessId"),
                ScalarBinaryOp.Gt,
                new LiteralScalar(i, LiteralKind.Int)));
        }
        node = new ProjectNode(node,
            [new ProjectionExpr("Timestamp", new ColumnRef("Timestamp"))]);
        node = new SortNode(node,
            [new SortExpr(new ColumnRef("Timestamp"), SortDirection.Desc)]);
        node = new LimitNode(node, 10);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "WITH");
        // Should have multiple CTE stages
        var stageCount = CTEPattern().Count(sql);
        Assert.IsGreaterThanOrEqualTo(8, stageCount, $"Expected 8+ CTE stages, got {stageCount}");
    }

    // ─── Empty and single-element collections ───────────────────────

    [TestMethod]
    [Description("Project with single column")]
    public void Project_SingleColumn()
    {
        var node = new ProjectNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("Timestamp", new ColumnRef("Timestamp"))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "SELECT Timestamp FROM");
    }

    [TestMethod]
    [Description("Aggregate with no GROUP BY (global aggregate)")]
    public void Aggregate_NoGroupBy()
    {
        var node = new AggregateNode(
            new ScanNode("DeviceProcessEvents"),
            Aggregates: [new ProjectionExpr("total", new FunctionCall("count", []))],
            GroupBy: []);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "count(*) AS total");
        Assert.DoesNotContain("GROUP BY", NormSql(sql),
            "Global aggregate should not have GROUP BY");
    }

    [TestMethod]
    [Description("Extend with multiple computed columns")]
    public void Extend_MultipleColumns()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [
                new ProjectionExpr("a", new FunctionCall("tolower", [new ColumnRef("FileName")])),
                new ProjectionExpr("b", new FunctionCall("toupper", [new ColumnRef("DeviceName")])),
                new ProjectionExpr("c", new LiteralScalar("constant", LiteralKind.String)),
            ]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "lower(FileName) AS a");
        AssertSqlContains(sql, "upper(DeviceName) AS b");
        AssertSqlContains(sql, "'constant' AS c");
    }

    // ─── Sort edge cases ────────────────────────────────────────────

    [TestMethod]
    [Description("Sort with multiple columns and mixed directions")]
    public void Sort_MultipleColumns()
    {
        var node = new SortNode(
            new ScanNode("DeviceProcessEvents"),
            [
                new SortExpr(new ColumnRef("DeviceName"), SortDirection.Asc),
                new SortExpr(new ColumnRef("Timestamp"), SortDirection.Desc),
            ]);

        var sql = _emitter.Emit(node);
        // DuckDB emitter includes explicit NULLS ordering. Check core ordering parts separately.
        AssertSqlContains(sql, "ORDER BY DeviceName ASC");
        AssertSqlContains(sql, "Timestamp DESC");
    }

    [TestMethod]
    [Description("Sort with NULLS FIRST")]
    public void Sort_NullsFirst()
    {
        var node = new SortNode(
            new ScanNode("DeviceProcessEvents"),
            [new SortExpr(new ColumnRef("FileName"), SortDirection.Asc, NullOrder.First)]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "NULLS FIRST");
    }

    [TestMethod]
    [Description("Sort with NULLS LAST")]
    public void Sort_NullsLast()
    {
        var node = new SortNode(
            new ScanNode("DeviceProcessEvents"),
            [new SortExpr(new ColumnRef("FileName"), SortDirection.Desc, NullOrder.Last)]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "NULLS LAST");
    }

    // ─── Nested expressions ─────────────────────────────────────────

    [TestMethod]
    [Description("Deeply nested binary expression")]
    public void Expr_DeeplyNested()
    {
        // (a == 1 AND b == 2) OR (c == 3 AND d == 4)
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new BinaryScalar(
                    new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Eq, new LiteralScalar(1, LiteralKind.Int)),
                    ScalarBinaryOp.And,
                    new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Eq, new LiteralScalar(2, LiteralKind.Int))),
                ScalarBinaryOp.Or,
                new BinaryScalar(
                    new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Eq, new LiteralScalar(3, LiteralKind.Int)),
                    ScalarBinaryOp.And,
                    new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Eq, new LiteralScalar(4, LiteralKind.Int)))));

        var sql = _emitter.Emit(node);
        // Should produce valid parenthesized SQL without stack overflow
        AssertSqlContains(sql, "AND");
        AssertSqlContains(sql, "OR");
    }

    [TestMethod]
    [Description("Unary NOT on compound expression")]
    public void Expr_NotCompound()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new UnaryScalar(
                ScalarUnaryOp.Not,
                new BinaryScalar(
                    new ColumnRef("FileName"),
                    ScalarBinaryOp.Eq,
                    new LiteralScalar("cmd.exe", LiteralKind.String))));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "NOT");
        AssertSqlContains(sql, "FileName = 'cmd.exe'");
    }

    [TestMethod]
    [Description("Nested function calls")]
    public void Expr_NestedFunctions()
    {
        // tolower(substring(FileName, 0, 5))
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("prefix",
                new FunctionCall("tolower",
                    [new FunctionCall("substring",
                        [new ColumnRef("FileName"),
                         new LiteralScalar(0, LiteralKind.Int),
                         new LiteralScalar(5, LiteralKind.Int)])]))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "lower(substring(FileName");
    }

    // ─── CASE expression ────────────────────────────────────────────

    [TestMethod]
    [Description("CASE expression with multiple branches")]
    public void Expr_CaseMultiBranch()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("severity",
                new CaseScalar(
                    [(new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Gt, new LiteralScalar(1000, LiteralKind.Int)),
                      new LiteralScalar("high", LiteralKind.String)),
                     (new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Gt, new LiteralScalar(100, LiteralKind.Int)),
                      new LiteralScalar("medium", LiteralKind.String))],
                    new LiteralScalar("low", LiteralKind.String)))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "CASE WHEN");
        AssertSqlContains(sql, "THEN 'high'");
        AssertSqlContains(sql, "THEN 'medium'");
        AssertSqlContains(sql, "ELSE 'low' END");
    }

    // ─── String operator emission ───────────────────────────────────

    [TestMethod]
    [Description("contains emits ILIKE with wildcards")]
    public void StringOp_Contains()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new ColumnRef("ProcessCommandLine"),
                ScalarBinaryOp.Contains,
                new LiteralScalar("password", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "ILIKE");
    }

    [TestMethod]
    [Description("startswith emits ILIKE with trailing wildcard")]
    public void StringOp_StartsWith()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new ColumnRef("FileName"),
                ScalarBinaryOp.StartsWith,
                new LiteralScalar("power", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "ILIKE");
    }

    // ─── Timespan literal variants ──────────────────────────────────
    // All timespan tests verify that ago(x) emits: current_timestamp - INTERVAL '...'
    // ago() is NOT in official DuckDB docs; current_timestamp - INTERVAL is the documented idiom.

    [TestMethod]
    [Description("ago(1d) emits current_timestamp - INTERVAL '1 days'")]
    public void Timespan_Days()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new ColumnRef("Timestamp"),
                ScalarBinaryOp.Gt,
                new FunctionCall("ago", [new LiteralScalar("1d", LiteralKind.Timespan)])));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "current_timestamp - INTERVAL '1 days'");
    }

    [TestMethod]
    [Description("Timespan 2h emits INTERVAL '2 hours'")]
    public void Timespan_Hours()
    {
        var lit = new LiteralScalar("2h", LiteralKind.Timespan);
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new ColumnRef("Timestamp"),
                ScalarBinaryOp.Gt,
                new FunctionCall("ago", [lit])));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "INTERVAL '2 hours'");
    }

    [TestMethod]
    [Description("Timespan 30m emits INTERVAL '30 minutes'")]
    public void Timespan_Minutes()
    {
        var lit = new LiteralScalar("30m", LiteralKind.Timespan);
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new ColumnRef("Timestamp"),
                ScalarBinaryOp.Gt,
                new FunctionCall("ago", [lit])));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "INTERVAL '30 minutes'");
    }

    [TestMethod]
    [Description("Timespan 10s emits INTERVAL '10 seconds'")]
    public void Timespan_Seconds()
    {
        var lit = new LiteralScalar("10s", LiteralKind.Timespan);
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new ColumnRef("Timestamp"),
                ScalarBinaryOp.Gt,
                new FunctionCall("ago", [lit])));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "INTERVAL '10 seconds'");
    }

    [TestMethod]
    [Description("Timespan 500ms emits INTERVAL '500 milliseconds'")]
    public void Timespan_Milliseconds()
    {
        var lit = new LiteralScalar("500ms", LiteralKind.Timespan);
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new ColumnRef("Timestamp"),
                ScalarBinaryOp.Gt,
                new FunctionCall("ago", [lit])));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "INTERVAL '500 milliseconds'");
    }

    // ─── Unsupported node types ─────────────────────────────────────

    [TestMethod]
    [Description("Unsupported RelNode type throws NotSupportedException")]
    public void Unsupported_RelNodeType() => Assert.ThrowsExactly<NotSupportedException>(() => {
        _emitter.Emit(new UnsupportedTestNode());
    });

    // ─── Identifier quoting ─────────────────────────────────────────

    [TestMethod]
    [Description("Column name with space gets quoted")]
    public void Ident_WithSpace()
    {
        var node = new ProjectNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("Device Name", new ColumnRef("Device Name"))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "\"Device Name\"");
    }

    [TestMethod]
    [Description("Column name starting with digit gets quoted")]
    public void Ident_StartsWithDigit()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("123col", new LiteralScalar(1, LiteralKind.Int))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "\"123col\"");
    }

    [TestMethod]
    [Description("Normal column name does NOT get quoted")]
    public void Ident_NormalNotQuoted()
    {
        var node = new ProjectNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("Timestamp", new ColumnRef("Timestamp"))]);

        var sql = _emitter.Emit(node);
        Assert.DoesNotContain("\"Timestamp\"", sql,
            "Normal identifiers should not be quoted");
    }

    // ─── Emitter reuse / state isolation ────────────────────────────

    [TestMethod]
    [Description("Emitter produces consistent output on repeated calls")]
    public void Reuse_ConsistentOutput()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 100);
        var node = new LimitNode(new ScanNode("DeviceProcessEvents"), 5);

        var sql1 = emitter.Emit(node);
        var sql2 = emitter.Emit(node);

        Assert.AreEqual(NormSql(sql1), NormSql(sql2),
            "Same input should produce identical SQL across calls");
    }

    [TestMethod]
    [Description("CTE stage counter resets between calls")]
    public void Reuse_StageCounterResets()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 100);

        // First call with deep pipeline
        var deep = new LimitNode(
            new FilterNode(
                new ScanNode("DeviceProcessEvents"),
                new BinaryScalar(new ColumnRef("ProcessId"), ScalarBinaryOp.Gt, new LiteralScalar(0, LiteralKind.Int))),
            10);
        emitter.Emit(deep);

        // Second call should start from stage 0 again
        var simple = new ScanNode("DeviceProcessEvents");
        var sql2 = emitter.Emit(simple);

        // Should not contain high stage numbers from first call
        Assert.DoesNotContain("__kql_stage_5", sql2,
            "Stage counter should reset between Emit() calls");
    }

    // ─── Join emission ──────────────────────────────────────────────

    [TestMethod]
    [Description("SEMI JOIN emitted for LeftSemi")]
    public void Join_SemiJoin()
    {
        var node = new JoinNode(
            new ScanNode("DeviceProcessEvents"),
            new ScanNode("DeviceProcessEvents"),
            JoinKind.LeftSemi,
            new BinaryScalar(new ColumnRef("DeviceName"), ScalarBinaryOp.Eq, new ColumnRef("DeviceName")));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "SEMI JOIN");
    }

    [TestMethod]
    [Description("ANTI JOIN emitted for LeftAnti")]
    public void Join_AntiJoin()
    {
        var node = new JoinNode(
            new ScanNode("DeviceProcessEvents"),
            new ScanNode("DeviceProcessEvents"),
            JoinKind.LeftAnti,
            new BinaryScalar(new ColumnRef("DeviceName"), ScalarBinaryOp.Eq, new ColumnRef("DeviceName")));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "ANTI JOIN");
    }

    [TestMethod]
    [Description("RIGHT JOIN emitted for RightOuter")]
    public void Join_RightOuterJoin()
    {
        var node = new JoinNode(
            new ScanNode("DeviceProcessEvents"),
            new ScanNode("DeviceProcessEvents"),
            JoinKind.RightOuter,
            new BinaryScalar(new ColumnRef("DeviceName"), ScalarBinaryOp.Eq, new ColumnRef("DeviceName")));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "RIGHT JOIN");
    }

    [TestMethod]
    [Description("FULL OUTER JOIN emitted for FullOuter")]
    public void Join_FullOuterJoin()
    {
        var node = new JoinNode(
            new ScanNode("DeviceProcessEvents"),
            new ScanNode("DeviceProcessEvents"),
            JoinKind.FullOuter,
            new BinaryScalar(new ColumnRef("DeviceName"), ScalarBinaryOp.Eq, new ColumnRef("DeviceName")));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "FULL OUTER JOIN");
    }

    [TestMethod]
    [Description("RIGHT SEMI JOIN emitted for RightSemi")]
    public void Join_RightSemiJoin()
    {
        var node = new JoinNode(
            new ScanNode("DeviceProcessEvents"),
            new ScanNode("DeviceProcessEvents"),
            JoinKind.RightSemi,
            new BinaryScalar(new ColumnRef("DeviceName"), ScalarBinaryOp.Eq, new ColumnRef("DeviceName")));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "RIGHT SEMI JOIN");
    }

    [TestMethod]
    [Description("RIGHT ANTI JOIN emitted for RightAnti")]
    public void Join_RightAntiJoin()
    {
        var node = new JoinNode(
            new ScanNode("DeviceProcessEvents"),
            new ScanNode("DeviceProcessEvents"),
            JoinKind.RightAnti,
            new BinaryScalar(new ColumnRef("DeviceName"), ScalarBinaryOp.Eq, new ColumnRef("DeviceName")));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "RIGHT ANTI JOIN");
    }

    [TestMethod]
    [Description("SampleNode emits USING SAMPLE reservoir(n ROWS)")]
    public void Tabular_Sample_EmitsReservoir()
    {
        var node = new SampleNode(new ScanNode("DeviceProcessEvents"), 7);
        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "USING SAMPLE reservoir(7 ROWS)");
    }

    // ─── Null test functions ────────────────────────────────────────

    [TestMethod]
    [Description("isempty emits (x IS NULL OR x = '')")]
    public void Func_IsEmpty()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new FunctionCall("isempty", [new ColumnRef("FileName")]));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "IS NULL OR");
        AssertSqlContains(sql, "= ''");
    }

    [TestMethod]
    [Description("isnull emits (x IS NULL)")]
    public void Func_IsNull()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new FunctionCall("isnull", [new ColumnRef("FileName")]));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "IS NULL");
    }

    [TestMethod]
    [Description("coalesce emits COALESCE")]
    public void Func_Coalesce()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("name",
                new FunctionCall("coalesce",
                    [new ColumnRef("DeviceName"),
                     new LiteralScalar("unknown", LiteralKind.String)]))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "COALESCE(DeviceName, 'unknown')");
    }

    [TestMethod]
    [Description("New trivial mappings enforce argument counts")]
    public void Func_TrivialMappings_InvalidArity_Rejected()
    {
        var node = new ProjectNode(
            new ScanNode("DeviceProcessEvents"),
            [
                new ProjectionExpr("a", new FunctionCall("strcat_array", [new ColumnRef("Tags")])),
                new ProjectionExpr("b", new FunctionCall("bag_keys", [new ColumnRef("AdditionalFields"), new LiteralScalar("extra", LiteralKind.String)])),
                new ProjectionExpr("c", new FunctionCall("bag_has_key", [new ColumnRef("AdditionalFields")])),
                new ProjectionExpr("d", new FunctionCall("bag_merge", [new ColumnRef("A")])),
                new ProjectionExpr("e", new FunctionCall("array_length", [])),
                new ProjectionExpr("f", new FunctionCall("exp2", [])),
                new ProjectionExpr("g", new FunctionCall("exp10", [new LiteralScalar(1, LiteralKind.Int), new LiteralScalar(2, LiteralKind.Int)]))
            ]);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _emitter.Emit(node));
    }

    // ─── StarExpr ───────────────────────────────────────────────────

    [TestMethod]
    [Description("StarExpr emits *")]
    public void Expr_Star()
    {
        var node = new ProjectNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("all", new StarExpr())]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "* AS all");
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private static string NormSql(string s) =>
        TrimPattern().Replace(s.Trim(), " ");

    private static void AssertSqlContains(string sql, string fragment)
    {
        var norm = NormSql(sql);
        var normFrag = NormSql(fragment);
        Assert.Contains(normFrag, norm,
            $"Expected SQL to contain '{normFrag}'\nActual: {norm}");
    }

    /// <summary>Dummy RelNode subclass the emitter doesn't know about.</summary>
    private sealed record UnsupportedTestNode() : RelNode;

    [GeneratedRegex(@"\bLIMIT\b")]
    private static partial Regex LimitPattern();

    [GeneratedRegex(@"__kql_stage_\d+")]
    private static partial Regex CTEPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex TrimPattern();
}
