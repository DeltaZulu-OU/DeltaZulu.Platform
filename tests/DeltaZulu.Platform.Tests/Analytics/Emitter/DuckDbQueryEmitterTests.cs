
using System.Globalization;
using System.Text.RegularExpressions;
using DeltaZulu.Platform.Application.Analytics.Translation;
using DeltaZulu.Platform.Data.DuckDb.Sql;
using DeltaZulu.Platform.Domain.Analytics.Catalog;
using DeltaZulu.Platform.Domain.Analytics.Policy;
using DeltaZulu.Platform.Domain.Analytics.QueryModel;
using DeltaZulu.Platform.Domain.Analytics.Schema;

namespace DeltaZulu.Platform.Tests.Analytics.Emitter;
/// <summary>
/// <para>
/// Red-green-refactor harness for RelNode → DuckDB SQL emission.
/// Each test constructs a RelNode tree directly and asserts the
/// emitted SQL string. SQL comparison normalizes whitespace.
/// </para>
/// <para>Source: Architecture spec, KQL-to-DuckDB translation spec (Appendix I).</para>
/// </summary>
[TestClass]
public sealed partial class DuckDbQueryEmitterTests
{
    private readonly DuckDbQueryEmitter _emitter = new(defaultLimit: 10_000);

    // ─── Basic operators ────────────────────────────────────────────

    [TestMethod]
    [Description("bin() over a numeric value uses floor arithmetic, not time_bucket")]
    public void Bin_NumericValue_UsesFloorArithmetic()
    {
        var node = new ExtendNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("b", new FunctionCall("bin",
                [new ColumnRef("ProcessId"), new LiteralScalar(10L, LiteralKind.Long)]))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "floor((ProcessId) / (10)) * (10)");
        Assert.DoesNotContain("time_bucket", NormSql(sql),
            "Numeric bin must not call time_bucket, which requires an INTERVAL argument");
    }

    [TestMethod]
    [Description("bin() over a datetime anchors time_bucket at the Unix epoch")]
    public void Bin_TimespanValue_AnchorsAtEpoch()
    {
        var node = new ExtendNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("b", new FunctionCall("bin",
                [new ColumnRef("Timestamp"), new LiteralScalar(TimeSpan.FromHours(1), LiteralKind.Timespan)]))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "time_bucket(INTERVAL '1 hours', Timestamp, TIMESTAMP '1970-01-01')");
    }

    [TestMethod]
    [Description("AggregateNode with count() by")]
    public void Emit_Aggregate_CountBy()
    {
        var node = new AggregateNode(
            new ScanNode("ProcessEvent"),
            Aggregates: [new ProjectionExpr("count_", new FunctionCall("count", []))],
            GroupBy: [new ColumnRef("FileName")]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "count(*) AS count_");
        AssertSqlContains(sql, "GROUP BY FileName");
    }

    [TestMethod]
    [Description("Full vertical slice: filter → project → limit")]
    public void Emit_Composed_VerticalSlice()
    {
        var node =
            new LimitNode(
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
                        new ProjectionExpr("ProcessCommandLine", new ColumnRef("ProcessCommandLine")),
                    ]),
                20);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "FileName = 'powershell.exe'");
        AssertSqlContains(sql, "Timestamp, DeviceName, ProcessCommandLine");
        AssertSqlContains(sql, "LIMIT 20");
        AssertSqlContains(sql, "golden.ProcessEvent");
    }

    [TestMethod]
    [Description("Multi-stage linear pipeline can collapse into a single SELECT block")]
    public void Emit_CteStaging_MultiStage()
    {
        var node =
            new LimitNode(
                new ProjectNode(
                    new FilterNode(
                        new ScanNode("ProcessEvent"),
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
        Assert.DoesNotContain("WITH", NormSql(sql), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("__kql_stage_", NormSql(sql), StringComparison.OrdinalIgnoreCase);
        AssertSqlContains(sql, "FileName = 'cmd.exe'");
        AssertSqlContains(sql, "SELECT Timestamp, DeviceName FROM golden.ProcessEvent");
        AssertSqlContains(sql, "LIMIT 10");
    }

    [TestMethod]
    [Description("ExtendNode adds computed column via SELECT *, expr AS alias")]
    public void Emit_Extend()
    {
        var node = new ExtendNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("lower_name",
                new FunctionCall("tolower", [new ColumnRef("FileName")]))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "lower(FileName) AS lower_name");
        AssertSqlContains(sql, "SELECT *,");
    }

    // ─── Extend (subquery nesting) ──────────────────────────────────
    [TestMethod]
    [Description("Chained ExtendNodes produce staged CTEs")]
    public void Emit_Extend_Chained()
    {
        var inner = new ExtendNode(
            new ScanNode("ProcessEvent"),
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

    [TestMethod]
    [Description("FilterNode with string equality")]
    public void Emit_Filter_StringEq()
    {
        var node = new FilterNode(
            new ScanNode("ProcessEvent"),
            new BinaryScalar(
                new ColumnRef("FileName"),
                ScalarBinaryOp.Eq,
                new LiteralScalar("powershell.exe", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "FileName = 'powershell.exe'");
        AssertSqlContains(sql, "golden.ProcessEvent");
    }

    [TestMethod]
    [Description("ago(7d) emits current_timestamp - INTERVAL (ago() not in official DuckDB docs v1.5)")]
    public void Emit_Func_Ago()
    {
        var node = new FilterNode(
            new ScanNode("ProcessEvent"),
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
    [Description("bag_has_key and array_slice mappings emit exact SQL shape")]
    public void Emit_Func_BagAndArrayMappings()
    {
        var node = new ExtendNode(
            new ScanNode("ProcessEvent"),
            [
                new ProjectionExpr("hask", new FunctionCall("bag_has_key", [new ColumnRef("AdditionalFields"), new LiteralScalar("User", LiteralKind.String)])),
                new ProjectionExpr("slice", new FunctionCall("array_slice", [new ColumnRef("Tags"), new LiteralScalar(0, LiteralKind.Int), new LiteralScalar(2, LiteralKind.Int)]))
            ]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "(json_extract(AdditionalFields, concat('$.', 'User')) IS NOT NULL) AS hask");
        AssertSqlContains(sql, "list_slice(Tags, (0) + 1, (2) - (0)) AS slice");
    }

    [TestMethod]
    [Description("extract wraps with COALESCE to preserve KQL empty-string-on-no-match")]
    public void Emit_Func_ExtractCoalesceWrap()
    {
        var node = new ExtendNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("user",
                new FunctionCall("extract",
                    [new LiteralScalar(@"User=(\w+)", LiteralKind.String),
                     new LiteralScalar(1, LiteralKind.Int),
                     new ColumnRef("ProcessCommandLine")]))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "COALESCE(regexp_extract(");
    }

    [TestMethod]
    [Description("hash_sha256, hash_md5, and KQL-compatible translate emit DuckDB scalar mappings")]
    public void Emit_Func_HashAndTranslateMappings()
    {
        var node = new ProjectNode(
            new SingletonRowNode(),
            [
                new ProjectionExpr("sha", new FunctionCall("hash_sha256", [new LiteralScalar("abc", LiteralKind.String)])),
                new ProjectionExpr("md5", new FunctionCall("hash_md5", [new LiteralScalar("abc", LiteralKind.String)])),
                new ProjectionExpr("translated", new FunctionCall("translate", [new LiteralScalar("abc", LiteralKind.String), new LiteralScalar("x", LiteralKind.String), new LiteralScalar("abc", LiteralKind.String)]))
            ]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "sha256('abc') AS sha");
        AssertSqlContains(sql, "md5('abc') AS md5");
        AssertSqlContains(sql, "translate('abc', 'abc', CASE WHEN 'x' = '' THEN '' ELSE rpad('x', CAST(length('abc') AS INTEGER), right('x', 1)) END) AS translated");
    }

    [TestMethod]
    public void Emit_Func_RandTrigFormatBytes()
    {
        var node = new ProjectNode(
            new SingletonRowNode(),
            [
                new ProjectionExpr("r", new FunctionCall("rand", [])),
                new ProjectionExpr("c", new FunctionCall("cos", [new LiteralScalar(0, LiteralKind.Int)])),
                new ProjectionExpr("s", new FunctionCall("sin", [new LiteralScalar(0, LiteralKind.Int)])),
                new ProjectionExpr("fb", new FunctionCall("format_bytes", [new LiteralScalar(2048, LiteralKind.Int)]))
            ]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "random() AS r");
        AssertSqlContains(sql, "cos(0) AS c");
        AssertSqlContains(sql, "sin(0) AS s");
        AssertSqlContains(sql, "KB");
    }

    [TestMethod]
    [Description("Shortlist function mappings emit DuckDB-native SQL")]
    public void Emit_Func_ShortlistMappings()
    {
        var node = new ExtendNode(
            new ScanNode("ProcessEvent"),
            [
                new ProjectionExpr("arr", new FunctionCall("strcat_array", [new ColumnRef("Tags"), new LiteralScalar(",", LiteralKind.String)])),
                new ProjectionExpr("b64e", new FunctionCall("base64_encode_tostring", [new ColumnRef("FileName")])),
                new ProjectionExpr("b64d", new FunctionCall("base64_decode_tostring", [new LiteralScalar("YQ==", LiteralKind.String)])),
                new ProjectionExpr("ue", new FunctionCall("url_encode", [new ColumnRef("FileName")])),
                new ProjectionExpr("ud", new FunctionCall("url_decode", [new ColumnRef("FileName")])),
                new ProjectionExpr("bk", new FunctionCall("bag_keys", [new ColumnRef("AdditionalFields")])),
                new ProjectionExpr("bhk", new FunctionCall("bag_has_key", [new ColumnRef("AdditionalFields"), new LiteralScalar("k", LiteralKind.String)])),
                new ProjectionExpr("bm", new FunctionCall("bag_merge", [new ColumnRef("A"), new ColumnRef("B")])),
                new ProjectionExpr("alen", new FunctionCall("array_length", [new ColumnRef("Tags")])),
                new ProjectionExpr("acon", new FunctionCall("array_concat", [new ColumnRef("Tags"), new ColumnRef("MoreTags")])),
                new ProjectionExpr("aslice", new FunctionCall("array_slice", [new ColumnRef("Tags"), new LiteralScalar(1, LiteralKind.Int), new LiteralScalar(3, LiteralKind.Int)])),
                new ProjectionExpr("e2", new FunctionCall("exp2", [new LiteralScalar(3, LiteralKind.Int)])),
                new ProjectionExpr("e10", new FunctionCall("exp10", [new LiteralScalar(2, LiteralKind.Int)]))
            ]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "array_to_string(Tags, ',') AS arr");
        AssertSqlContains(sql, "to_base64(CAST(FileName AS BLOB)) AS b64e");
        AssertSqlContains(sql, "CAST(from_base64('YQ==') AS VARCHAR) AS b64d");
        AssertSqlContains(sql, "url_encode(FileName) AS ue");
        AssertSqlContains(sql, "url_decode(FileName) AS ud");
        AssertSqlContains(sql, "json_keys(AdditionalFields) AS bk");
        AssertSqlContains(sql, "(json_extract(AdditionalFields, concat('$.', 'k')) IS NOT NULL) AS bhk");
        AssertSqlContains(sql, "json_merge_patch(A, B) AS bm");
        AssertSqlContains(sql, "CASE WHEN json_valid(CAST(Tags AS VARCHAR)) THEN json_array_length(Tags) ELSE length(Tags) END AS alen");
        AssertSqlContains(sql, "list_concat(Tags, MoreTags) AS acon");
        AssertSqlContains(sql, "list_slice(Tags, (1) + 1, (3) - (1)) AS aslice");
        AssertSqlContains(sql, "power(2, 3) AS e2");
        AssertSqlContains(sql, "power(10, 2) AS e10");
    }

    // ─── Aggregate ──────────────────────────────────────────────────
    // ─── Function mapping ───────────────────────────────────────────
    [TestMethod]
    [Description("tolower → lower, strlen → length")]
    public void Emit_Func_StringMappings()
    {
        var node = new ExtendNode(
            new ScanNode("ProcessEvent"),
            [
                new ProjectionExpr("a", new FunctionCall("tolower", [new ColumnRef("FileName")])),
                new ProjectionExpr("b", new FunctionCall("strlen", [new ColumnRef("FileName")])),
            ]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "lower(FileName) AS a");
        AssertSqlContains(sql, "length(FileName) AS b");
    }

    [TestMethod]
    [Description("Additional trivial function mappings emit expected DuckDB SQL")]
    public void Emit_Func_TrivialMappings()
    {
        var node = new ExtendNode(
            new ScanNode("ProcessEvent"),
            [
                new ProjectionExpr("idx", new FunctionCall("indexof", [new ColumnRef("FileName"), new LiteralScalar("a", LiteralKind.String)])),
                new ProjectionExpr("rev", new FunctionCall("reverse", [new ColumnRef("FileName")])),
                new ProjectionExpr("mx", new FunctionCall("max_of", [new ColumnRef("A"), new ColumnRef("B")])),
                new ProjectionExpr("mn", new FunctionCall("min_of", [new ColumnRef("A"), new ColumnRef("B")])),
                new ProjectionExpr("dec", new FunctionCall("todecimal", [new ColumnRef("A")])),
                new ProjectionExpr("gid", new FunctionCall("toguid", [new ColumnRef("FileName")])),
                new ProjectionExpr("nan", new FunctionCall("isnan", [new ColumnRef("A")])),
                new ProjectionExpr("inf", new FunctionCall("isinf", [new ColumnRef("A")])),
                new ProjectionExpr("ceilv", new FunctionCall("ceiling", [new ColumnRef("A")])),
                new ProjectionExpr("floorv", new FunctionCall("floor", [new ColumnRef("A")])),
                new ProjectionExpr("roundv", new FunctionCall("round", [new ColumnRef("A"), new LiteralScalar(2, LiteralKind.Int)])),
                new ProjectionExpr("logv", new FunctionCall("log", [new ColumnRef("A")])),
                new ProjectionExpr("powv", new FunctionCall("pow", [new ColumnRef("A"), new LiteralScalar(2, LiteralKind.Int)])),
                new ProjectionExpr("piv", new FunctionCall("pi", [])),
            ]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "(strpos(FileName, 'a') - 1) AS idx");
        AssertSqlContains(sql, "reverse(FileName) AS rev");
        AssertSqlContains(sql, "greatest(A, B) AS mx");
        AssertSqlContains(sql, "least(A, B) AS mn");
        AssertSqlContains(sql, "CAST(A AS DECIMAL) AS dec");
        AssertSqlContains(sql, "CAST(FileName AS VARCHAR) AS gid");
        AssertSqlContains(sql, "isnan(A) AS nan");
        AssertSqlContains(sql, "isinf(A) AS inf");
        AssertSqlContains(sql, "ceil(A) AS ceilv");
        AssertSqlContains(sql, "floor(A) AS floorv");
        AssertSqlContains(sql, "round(A, 2) AS roundv");
        AssertSqlContains(sql, "ln(A) AS logv");
        AssertSqlContains(sql, "power(A, 2) AS powv");
        AssertSqlContains(sql, "pi() AS piv");
    }

    [TestMethod]
    [Description("LimitNode wrapping ScanNode")]
    public void Emit_Limit()
    {
        var node = new LimitNode(new ScanNode("ProcessEvent"), 20);
        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "LIMIT 20");
        // Should NOT have default limit when user specifies one
        Assert.DoesNotContain("LIMIT 10000", NormSql(sql), "Default limit should not appear when user specifies LIMIT");
    }

    [TestMethod]
    [Description("ProjectNode selects named columns")]
    public void Emit_Project()
    {
        var node = new ProjectNode(
            new ScanNode("ProcessEvent"),
            [
                new ProjectionExpr("Timestamp", new ColumnRef("Timestamp")),
                new ProjectionExpr("DeviceName", new ColumnRef("DeviceName")),
            ]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "SELECT Timestamp, DeviceName FROM");
    }

    [TestMethod]
    [Description("sample-distinct emits a compact SELECT DISTINCT ... LIMIT shape without transient stages")]
    public void Emit_SampleDistinct()
    {
        var node = new SampleNode(
            new DistinctNode(
                new ScanNode("ProcessEvent"),
                [new ProjectionExpr("sample_distinct_value", new ColumnRef("FileName"))]),
            7);

        var sql = _emitter.Emit(node);
        var norm = NormSql(sql);

        AssertSqlContains(sql, "SELECT DISTINCT FileName FROM golden.ProcessEvent LIMIT 7");
        Assert.DoesNotContain("WITH", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatchRegex(@"__kql_stage_\d+", norm);
        Assert.DoesNotContain("USING SAMPLE", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sample_distinct_value", norm, StringComparison.OrdinalIgnoreCase);
        Assert.AreEqual(0, _emitter.LastRunStats?.FinalCteCount);
    }

    [TestMethod]
    [Description("sample-distinct KQL pipeline emits compact DISTINCT/LIMIT SQL without transient stages")]
    public void Emit_SampleDistinct_KqlPipeline_CollapsesToDistinctLimit()
    {
        var catalog = new ApprovedViewCatalog();
        catalog.RegisterAll(SchemaConventions.CanonicalViews);
        var diagnostics = new DiagnosticBag();
        var translator = new KustoToRelational(catalog, diagnostics);

        var plan = translator.Translate("ProcessEvent | sample-distinct 10 of FileName");

        Assert.IsFalse(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.All));
        Assert.IsNotNull(plan);

        var sql = _emitter.Emit(plan);
        var norm = NormSql(sql);

        AssertSqlContains(sql, "SELECT DISTINCT FileName FROM golden.ProcessEvent LIMIT 10");
        Assert.DoesNotContain("WITH", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatchRegex(@"__kql_stage_\d+", norm);
        Assert.DoesNotContain("USING SAMPLE", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sample_distinct_value", norm, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    [Description("sample-distinct after a simple where folds the filter into the compact DISTINCT/LIMIT shape")]
    public void Emit_SampleDistinct_FilteredScan()
    {
        var node = new SampleNode(
            new DistinctNode(
                new FilterNode(
                    new ScanNode("ProcessEvent"),
                    new BinaryScalar(
                        new ColumnRef("FileName"),
                        ScalarBinaryOp.Has,
                        new LiteralScalar("powershell", LiteralKind.String))),
                [new ProjectionExpr("sample_distinct_value", new ColumnRef("FileName"))]),
            10);

        var sql = _emitter.Emit(node);
        var norm = NormSql(sql);

        AssertSqlContains(sql, "SELECT DISTINCT FileName FROM golden.ProcessEvent WHERE");
        Assert.Contains("regexp_matches(lower(FileName)", norm, StringComparison.Ordinal);
        Assert.MatchesRegex(@"\bLIMIT\s+10\s*;?\s*$", norm);
        Assert.DoesNotContain("WITH", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatchRegex(@"__kql_stage_\d+", norm);
        Assert.DoesNotContain("sample_distinct_value", norm, StringComparison.OrdinalIgnoreCase);
        Assert.AreEqual(0, _emitter.LastRunStats?.FinalCteCount);
    }

    [TestMethod]
    [Description("generic distinct|sample is not rewritten as sample-distinct without the translator marker alias")]
    public void Emit_DistinctThenSample_NonSampleDistinctAlias_RemainsReservoirSample()
    {
        var node = new SampleNode(
            new DistinctNode(
                new ScanNode("ProcessEvent"),
                [new ProjectionExpr("FileName", new ColumnRef("FileName"))]),
            7);

        var sql = _emitter.Emit(node);
        var norm = NormSql(sql);

        AssertSqlContains(sql, "SELECT DISTINCT FileName FROM golden.ProcessEvent");
        AssertSqlContains(sql, "USING SAMPLE reservoir(7 ROWS)");
        Assert.Contains("WITH", norm, StringComparison.OrdinalIgnoreCase);
        Assert.MatchesRegex(@"__kql_stage_\d+", norm);
    }

    [TestMethod]
    [Description("ScanNode emits FROM golden.<view>")]
    public void Emit_Scan()
    {
        var node = new ScanNode("ProcessEvent");
        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "FROM golden.ProcessEvent");
        AssertSqlContains(sql, "LIMIT 10000"); // default safety limit
    }

    [TestMethod]
    [Description("sort|take over base scan: terminal top-k remains outermost and does not reference removed source CTE")]
    public void Emit_Simplification_ScanSortTake_NoDanglingStageReference()
    {
        var node = new LimitNode(
            new SortNode(
                new ScanNode("NetworkSession"),
                [new SortExpr(new ColumnRef("RemoteIP"), SortDirection.Desc)]),
            5);

        var sql = _emitter.Emit(node);
        var norm = NormSql(sql);
        Assert.MatchesRegex(@"ORDER\s+BY\s+RemoteIP\s+DESC\s+NULLS\s+LAST\s+LIMIT\s+5\s*;?\s*$", norm);
        Assert.DoesNotMatchRegex(
            @"__kql_stage_\d+\s+AS\s*\(\s*SELECT\s+\*\s+FROM\s+main\.NetworkSession\s*\)",
            norm);
        AssertSqlContains(sql, "ORDER BY RemoteIP DESC NULLS LAST LIMIT 5");
    }

    [TestMethod]
    [Description("summarize|sort|take: no pass-through CTEs, fused ORDER BY+LIMIT, count_ has no redundant NULLS modifier")]
    public void Emit_Simplification_TopKPipeline()
    {
        var node = new LimitNode(
            new SortNode(
                new AggregateNode(
                    new ScanNode("NetworkSession"),
                    Aggregates: [new ProjectionExpr("count_", new FunctionCall("count", []))],
                    GroupBy: [new ColumnRef("RemoteIP"), new ColumnRef("RemotePort")]),
                [new SortExpr(new ColumnRef("count_"), SortDirection.Desc)]),
            20);
        var sql = _emitter.Emit(node);
        var norm = NormSql(sql);
        var passThrough = PassthroughPattern().Count(norm);
        Assert.AreEqual(0, passThrough);
        Assert.DoesNotContain("WITH", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("__kql_stage_", norm, StringComparison.OrdinalIgnoreCase);
        AssertSqlContains(sql, "ORDER BY count_ DESC LIMIT 20");
        Assert.MatchesRegex(@"ORDER\s+BY\s+count_\s+DESC\s+LIMIT\s+20\s*;?\s*$", norm);
        Assert.DoesNotContain("NULLS LAST", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatchRegex(
            @"__kql_stage_\d+\s+AS\s*\(\s*SELECT\s+\*\s+FROM\s+main\.NetworkSession\s*\)",
            norm);
        Assert.DoesNotMatchRegex(@"SELECT\s+\*\s+FROM\s+__kql_stage_\d+\s*;?\s*$", norm);
        AssertSqlContains(sql, "SELECT RemoteIP, RemotePort, count(*) AS count_");
        AssertSqlContains(sql, "FROM golden.NetworkSession");
        AssertSqlContains(sql, "GROUP BY RemoteIP, RemotePort");
    }

    [TestMethod]
    [Description("SortNode with desc direction")]
    public void Emit_Sort_Desc()
    {
        var node = new SortNode(
            new ScanNode("ProcessEvent"),
            [new SortExpr(new ColumnRef("Timestamp"), SortDirection.Desc)]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "ORDER BY Timestamp DESC NULLS LAST");
    }

    [TestMethod]
    [Description("KQL sort default is desc; emitter must emit DESC explicitly")]
    public void Emit_Sort_KqlDefaultDesc()
    {
        var node = new SortNode(
            new ScanNode("ProcessEvent"),
            [new SortExpr(new ColumnRef("Timestamp"), SortDirection.Desc)]);

        var sql = _emitter.Emit(node);
        // Must contain explicit DESC — never rely on DuckDB default (ASC)
        AssertSqlContains(sql, "ORDER BY Timestamp DESC NULLS LAST");
    }

    [TestMethod]
    [Description("WindowScalarExpr(sum) with ROWS UNBOUNDED PRECEDING → cumulative sum")]
    public void Emit_Window_CumulativeSum()
    {
        var node = new ExtendNode(
            new ScanNode("ProcessEvent"),
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
    [Description("WindowScalarExpr(lag) → lag(...) OVER (ORDER BY ...)")]
    public void Emit_Window_Lag()
    {
        var node = new ExtendNode(
            new ScanNode("ProcessEvent"),
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

    // ─── Window functions ────────────────────────────────────────
    [TestMethod]
    [Description("WindowScalarExpr(lead) → lead(...) OVER (ORDER BY ...)")]
    public void Emit_Window_Lead()
    {
        var node = new ExtendNode(
            new ScanNode("ProcessEvent"),
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
    [Description("Sliding window: RANGE BETWEEN INTERVAL PRECEDING AND CURRENT ROW")]
    public void Emit_Window_RangeFrame()
    {
        var node = new ExtendNode(
            new ScanNode("ProcessEvent"),
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

    [TestMethod]
    [Description("WindowScalarExpr(row_number) → row_number() OVER (ORDER BY ...)")]
    public void Emit_Window_RowNumber()
    {
        var node = new ExtendNode(
            new ScanNode("ProcessEvent"),
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
    [Description("WindowScalarExpr with PARTITION BY")]
    public void Emit_Window_WithPartition()
    {
        var node = new ExtendNode(
            new ScanNode("ProcessEvent"),
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
    [Description("extend over scan keeps staged alias dependency for chained extend expressions")]
    public void ExtendExtend_OptimizedMode_PreservesAliasDependency()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 10_000, applyDefaultLimit: false);
        var node = new ExtendNode(
            new ExtendNode(
                new ScanNode("ProcessEvent"),
                [
                    new ProjectionExpr("lower_name", new FunctionCall("tolower", [new ColumnRef("FileName")]))
                ]),
            [
                new ProjectionExpr("name_len", new FunctionCall("strlen", [new ColumnRef("lower_name")]))
            ]);

        var sql = emitter.Emit(node);
        var norm = NormSql(sql);

        Assert.Contains("lower(FileName) AS lower_name", norm, StringComparison.Ordinal);
        Assert.Contains("length(lower_name) AS name_len", norm, StringComparison.Ordinal);
        Assert.MatchesRegex(@"FROM\s+__kql_stage_\d+", norm);
        Assert.DoesNotContain("length(lower(FileName) AS lower_name)", norm, StringComparison.Ordinal);
    }

    [TestMethod]
    [Description("simple extend|project|take preserves computed alias and explicit limit")]
    public void ExtendProjectTake_OptimizedMode_PreservesComputedAliasAndLimit()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 10_000, applyDefaultLimit: false);
        var node = new LimitNode(
            new ProjectNode(
                new ExtendNode(
                    new ScanNode("ProcessEvent"),
                    [
                        new ProjectionExpr("lower_name", new FunctionCall("tolower", [new ColumnRef("FileName")]))
                    ]),
                [
                    new ProjectionExpr("Timestamp", new ColumnRef("Timestamp")),
                    new ProjectionExpr("lower_name", new ColumnRef("lower_name"))
                ]),
            10);

        var sql = emitter.Emit(node);
        var norm = NormSql(sql);

        Assert.Contains("lower(FileName) AS lower_name", norm, StringComparison.Ordinal);
        Assert.Contains("SELECT Timestamp, lower_name FROM", norm, StringComparison.Ordinal);
        Assert.MatchesRegex(@"\bLIMIT\s+10\s*;?\s*$", norm);
        Assert.DoesNotContain("LIMIT 10000", norm, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    [Description("countof(s, search) emits length-delta formula")]
    public void Func_CountOf()
    {
        var node = new ProjectNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("c", new FunctionCall("countof", [new ColumnRef("FileName"), new LiteralScalar("exe", LiteralKind.String)]))]);
        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "length(FileName)");
        AssertSqlContains(sql, "replace(FileName, 'exe', '')");
        AssertSqlContains(sql, "nullif(length('exe'), 0)");
    }

    [TestMethod]
    [Description("decimal(...) emits CAST(... AS DECIMAL)")]
    public void Func_Decimal()
    {
        var node = new ProjectNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("d", new FunctionCall("decimal", [new LiteralScalar(1.5, LiteralKind.Real)]))]);
        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "CAST(1.5 AS DECIMAL)");
    }

    // ─── Pipeline simplification ────────────────────────────────────
    [TestMethod]
    [Description("guid(...) emits TRY_CAST(... AS UUID)")]
    public void Func_Guid()
    {
        var node = new ProjectNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("gid", new FunctionCall("guid", [new LiteralScalar("74be27de-1e4e-49d9-b579-fe0b331d3642", LiteralKind.String)]))]);
        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "TRY_CAST('74be27de-1e4e-49d9-b579-fe0b331d3642' AS UUID)");
    }

    [TestMethod]
    [Description("percentile outside summarize is rejected by emitter")]
    public void Func_Percentile_OutsideAggregate_Rejected()
    {
        var node = new ProjectNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("p", new FunctionCall("percentile", [new ColumnRef("ProcessId"), new LiteralScalar(95, LiteralKind.Int)]))]);
        Assert.ThrowsExactly<NotSupportedException>(() => _emitter.Emit(node));
    }

    [TestMethod]
    [Description("Unknown function names are rejected")]
    public void Func_Unknown_ThrowsNotSupported() => Assert.ThrowsExactly<NotSupportedException>(() =>
                                                                                                          _emitter.Emit(new ExtendNode(
                                                                                                              new ScanNode("ProcessEvent"),
                                                                                                              [new ProjectionExpr("r", new FunctionCall("custom_function_xyz",
                    [new ColumnRef("FileName"), new LiteralScalar(42, LiteralKind.Int)]))])));

    [TestMethod]
    [Description("trim_start/trim_end, percentile, parse_path emit expected SQL")]
    public void Funcs_NewMappings_Emit()
    {
        var node = new ProjectNode(
            new ScanNode("ProcessEvent"),
            [
                new ProjectionExpr("ts", new FunctionCall("trim_start", [new LiteralScalar("\\\\+", LiteralKind.String), new ColumnRef("FolderPath")])),
                new ProjectionExpr("te", new FunctionCall("trim_end", [new LiteralScalar("\\\\+", LiteralKind.String), new ColumnRef("FolderPath")])),
                new ProjectionExpr("pp", new FunctionCall("parse_path", [new ColumnRef("FolderPath")])),
                new ProjectionExpr("b64e", new FunctionCall("base64_encode_tostring", [new ColumnRef("FileName")])),
                new ProjectionExpr("b64d", new FunctionCall("base64_decode_tostring", [new LiteralScalar("YQ==", LiteralKind.String)]))
            ]);
        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "regexp_replace(FolderPath, concat('^(', '\\\\+', ')'), '')");
        AssertSqlContains(sql, "regexp_replace(FolderPath, concat('(', '\\\\+', ')$'), '')");
        AssertSqlContains(sql, "to_json(\n\tstruct_pack(");
        AssertSqlContains(sql, "to_base64(CAST(FileName AS BLOB))");
        AssertSqlContains(sql, "CAST(from_base64('YQ==') AS VARCHAR)");

        var aggNode = new AggregateNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("p95", new FunctionCall("percentile", [new ColumnRef("ProcessId"), new LiteralScalar(95, LiteralKind.Int)]))],
            []);
        var aggSql = _emitter.Emit(aggNode);
        AssertSqlContains(aggSql, "quantile_cont(ProcessId, (95) / 100.0) AS p95");
    }

    // ─── Spec-derived: CTE staging ──────────────────────────────────
    // ─── Spec-derived: sort default direction ────────────────────────
    // ─── Spec-derived: extract COALESCE wrapping ─────────────────────
    // ─── Composed pipeline ──────────────────────────────────────────
    // ─── Let binding semantics ─────────────────────────────────────

    [TestMethod]
    [Description("Join ON qualifies each side with its table alias")]
    public void Join_On_EqualityPredicate()
    {
        var node = new JoinNode(
            new ScanNode("ProcessEvent"),
            new ScanNode("ProcessEvent"),
            JoinKind.Inner,
            new BinaryScalar(
                new ColumnRef("DeviceName", JoinSide.Left),
                ScalarBinaryOp.Eq,
                new ColumnRef("DeviceName", JoinSide.Right)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "INNER JOIN");
        // Both sides must be qualified so a self-join is not ambiguous /
        // an always-true tautology.
        AssertSqlContains(sql, "ON (__join_left.DeviceName = __join_right.DeviceName)");
    }

    [TestMethod]
    [Description("Join output still gets default safety limit")]
    public void Join_StillGetsSafetyCap()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 100);
        var node = new JoinNode(
            new ScanNode("ProcessEvent"),
            new LimitNode(new ScanNode("ProcessEvent"), 5),
            JoinKind.Inner,
            new BinaryScalar(
                new ColumnRef("DeviceName", JoinSide.Left),
                ScalarBinaryOp.Eq,
                new ColumnRef("DeviceName", JoinSide.Right)));

        var sql = emitter.Emit(node);
        AssertSqlContains(sql, "LIMIT 100");
    }

    [TestMethod]
    [Description("Scalar let values are inlined inside predicate")]
    public void Let_Scalar_Inlined()
    {
        var node = new LetBindingNode(
            Name: "cutoff",
            TabularValue: null,
            ScalarValue: new FunctionCall("ago", [new LiteralScalar("7d", LiteralKind.Timespan)]),
            Body: new FilterNode(
                new ScanNode("ProcessEvent"),
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
            TabularValue: null,
            ScalarValue: new LiteralScalar(42, LiteralKind.Int),
            Body: new ScanNode("ProcessEvent")));

        var sql = _emitter.Emit(new FilterNode(
            new ScanNode("ProcessEvent"),
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
                new ScanNode("ProcessEvent"),
                new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.Eq,
                    new LiteralScalar("powershell.exe", LiteralKind.String))),
            ScalarValue: null,
            Body: new ScanNode("ProcessEvent"));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "WITH");
        AssertSqlContains(sql, "PowerShellProcs");
        AssertSqlContains(sql, "powershell.exe");
    }

    [TestMethod]
    [Description("Lookup-shaped aggregate join emits explicit projection without SELECT * join wrappers")]
    public void Lookup_AggregateProjectSortTake_UsesExplicitJoinProjection()
    {
        var left = new AggregateNode(
            new ScanNode("ProcessEvent"),
            Aggregates: [new ProjectionExpr("LaunchCount", new FunctionCall("count", []))],
            GroupBy: [new ColumnRef("AccountName")]);

        var right = new AggregateNode(
            new ScanNode("ProcessEvent"),
            Aggregates: [new ProjectionExpr("DeviceCount", new FunctionCall("dcount", [new ColumnRef("DeviceName")]))],
            GroupBy: [new ColumnRef("AccountName")]);

        var join = new JoinNode(
            left,
            right,
            JoinKind.LeftOuter,
            new BinaryScalar(
                new ColumnRef("AccountName", JoinSide.Left),
                ScalarBinaryOp.Eq,
                new ColumnRef("AccountName", JoinSide.Right)),
            JoinFlavor.Lookup);

        var project = new ProjectNode(
            join,
            [
                new ProjectionExpr("AccountName", new ColumnRef("AccountName")),
            new ProjectionExpr("LaunchCount", new ColumnRef("LaunchCount")),
            new ProjectionExpr("DeviceCount", new ColumnRef("DeviceCount"))
            ]);

        var sort = new SortNode(
            project,
            [new SortExpr(new ColumnRef("LaunchCount"), SortDirection.Desc)]);

        var node = new LimitNode(sort, 25);
        var sql = _emitter.Emit(node);

        AssertSqlContains(sql, "left_agg.AccountName AS AccountName");
        AssertSqlContains(sql, "left_agg.LaunchCount AS LaunchCount");
        AssertSqlContains(sql, "right_agg.DeviceCount AS DeviceCount");
        AssertSqlContains(sql, "LEFT JOIN");
        AssertSqlContains(sql, "ON left_agg.AccountName = right_agg.AccountName");
        AssertSqlContains(sql, "ORDER BY left_agg.LaunchCount DESC NULLS LAST");
        AssertSqlContains(sql, "LIMIT 25");

        Assert.DoesNotContain(
            "SELECT * FROM __kql_stage_4",
            NormSql(sql),
            "Projected lookup join must not be wrapped by a final SELECT * stage.");
    }

    [TestMethod]
    public void Lookup_AggregateSubquery_ProjectSortTake_TranslationPreservesProjectColumns()
    {
        var catalog = new ApprovedViewCatalog();
        catalog.RegisterAll(SchemaConventions.CanonicalViews);

        var diagnostics = new DiagnosticBag();
        var translator = new KustoToRelational(catalog, diagnostics);

        var plan = translator.Translate(
            """
        ProcessEvent
        | summarize LaunchCount = count() by AccountName
        | lookup (ProcessEvent | summarize DeviceCount = dcount(DeviceName) by AccountName) on AccountName
        | project AccountName, LaunchCount, DeviceCount
        | sort by LaunchCount desc
        | take 25
        """);

        Assert.IsFalse(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.All));
        Assert.IsNotNull(plan);

        var project = FindFirst<ProjectNode>(plan!);

        Assert.IsNotNull(project);

        CollectionAssert.AreEqual(
            new[] { "AccountName", "LaunchCount", "DeviceCount" },
            project!.Projections.Select(p => p.Alias).ToArray());
    }

    [TestMethod]
    [Description("Lookup-flavored aggregate join collapses project+join into one qualified, unambiguous SELECT")]
    public void Lookup_FlavorAggregateProjectSortTake_InlinesQualifiedJoin()
    {
        var left = new AggregateNode(
            new ScanNode("ProcessEvent"),
            Aggregates: [new ProjectionExpr("LaunchCount", new FunctionCall("count", []))],
            GroupBy: [new ColumnRef("AccountName")]);

        var right = new AggregateNode(
            new ScanNode("ProcessEvent"),
            Aggregates: [new ProjectionExpr("DeviceCount", new FunctionCall("dcount", [new ColumnRef("DeviceName")]))],
            GroupBy: [new ColumnRef("AccountName")]);

        var join = new JoinNode(
            left,
            right,
            JoinKind.LeftOuter,
            new BinaryScalar(
                new ColumnRef("AccountName", JoinSide.Left),
                ScalarBinaryOp.Eq,
                new ColumnRef("AccountName", JoinSide.Right)),
            JoinFlavor.Lookup);

        var project = new ProjectNode(
            join,
            [
                new ProjectionExpr("AccountName", new ColumnRef("AccountName")),
            new ProjectionExpr("LaunchCount", new ColumnRef("LaunchCount")),
            new ProjectionExpr("DeviceCount", new ColumnRef("DeviceCount"))
            ]);

        var sort = new SortNode(
            project,
            [new SortExpr(new ColumnRef("LaunchCount"), SortDirection.Desc)]);

        var node = new LimitNode(sort, 25);
        var sql = _emitter.Emit(node);

        AssertSqlContains(sql, "WITH");
        AssertSqlContains(sql, "left_agg AS (");
        AssertSqlContains(sql, "right_agg AS (");
        AssertSqlContains(sql, "left_agg.AccountName AS AccountName");
        AssertSqlContains(sql, "left_agg.LaunchCount AS LaunchCount");
        AssertSqlContains(sql, "right_agg.DeviceCount AS DeviceCount");
        AssertSqlContains(sql, "FROM left_agg LEFT JOIN right_agg");
        AssertSqlContains(sql, "ON left_agg.AccountName = right_agg.AccountName");
        AssertSqlContains(sql, "ORDER BY left_agg.LaunchCount DESC NULLS LAST");
        AssertSqlContains(sql, "LIMIT 25");

        Assert.DoesNotContain(
            "SELECT *",
            NormSql(sql),
            "Clean projected lookup aggregate rendering must not emit SELECT * wrappers.");
    }

    [TestMethod]
    [Description("make_datetime with fewer than 6 args pads to make_timestamp's six")]
    public void MakeDatetime_ThreeArgs_PadsToSix()
    {
        var node = new ExtendNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("d", new FunctionCall("make_datetime",
                [
                    new LiteralScalar(2023L, LiteralKind.Long),
                    new LiteralScalar(1L, LiteralKind.Long),
                    new LiteralScalar(15L, LiteralKind.Long)
                ]))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "make_timestamp(2023, 1, 15, 0, 0, 0.0)");
    }

    [TestMethod]
    [Description("has_cs uses case-sensitive regex without lower()")]
    public void Op_HasCs_CaseSensitiveRegex()
    {
        var node = new FilterNode(
            new ScanNode("ProcessEvent"),
            new BinaryScalar(new ColumnRef("ProcessCommandLine"), ScalarBinaryOp.HasCs,
                new LiteralScalar("Mimikatz", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "regexp_matches(ProcessCommandLine");
        AssertSqlContains(sql, "'c'");
        Assert.DoesNotContain("lower(ProcessCommandLine)", NormSql(sql));
    }

    [TestMethod]
    [Description("hasprefix uses leading boundary only")]
    public void Op_HasPrefix_UsesLeadingBoundaryOnly()
    {
        var node = new FilterNode(
            new ScanNode("ProcessEvent"),
            new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.HasPrefix,
                new LiteralScalar("power", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "regexp_matches(lower(FileName)");
        AssertSqlContains(sql, "(^|[^[:alnum:]])");
        Assert.DoesNotContain("([^[:alnum:]]|$)", sql);
    }

    [TestMethod]
    [Description("hasprefix_cs uses case-sensitive flag and no lower()")]
    public void Op_HasPrefixCs_CaseSensitiveNoLowering()
    {
        var node = new FilterNode(
            new ScanNode("ProcessEvent"),
            new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.HasPrefixCs,
                new LiteralScalar("Power", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "regexp_matches(FileName");
        AssertSqlContains(sql, "'c'");
        Assert.DoesNotContain("lower(FileName)", sql);
    }

    [TestMethod]
    [Description("hassuffix uses trailing boundary only")]
    public void Op_HasSuffix_UsesTrailingBoundaryOnly()
    {
        var node = new FilterNode(
            new ScanNode("ProcessEvent"),
            new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.HasSuffix,
                new LiteralScalar("shell", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "regexp_matches(lower(FileName)");
        AssertSqlContains(sql, "([^[:alnum:]]|$)");
        Assert.DoesNotContain("(^|[^[:alnum:]])", sql);
    }

    [TestMethod]
    [Description("hassuffix_cs uses case-sensitive flag and no lower()")]
    public void Op_HasSuffixCs_CaseSensitiveNoLowering()
    {
        var node = new FilterNode(
            new ScanNode("ProcessEvent"),
            new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.HasSuffixCs,
                new LiteralScalar("Shell", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "regexp_matches(FileName");
        AssertSqlContains(sql, "'c'");
        Assert.DoesNotContain("lower(FileName)", sql);
    }

    [TestMethod]
    [Description("matches regex uses regexp_matches with case-sensitive flag")]
    public void Op_MatchesRegex_UsesCaseSensitiveFlag()
    {
        var node = new FilterNode(
            new ScanNode("ProcessEvent"),
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
            new ScanNode("ProcessEvent"),
            new BinaryScalar(new ColumnRef("ProcessCommandLine"), ScalarBinaryOp.NotHas,
                new LiteralScalar("mimikatz", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "NOT regexp_matches(lower(");
        AssertSqlContains(sql, "[^[:alnum:]]");
    }

    [TestMethod]
    [Description("not hasprefix negates leading-boundary regex")]
    public void Op_NotHasPrefix_NegatesLeadingBoundaryRegex()
    {
        var node = new FilterNode(
            new ScanNode("ProcessEvent"),
            new BinaryScalar(new ColumnRef("FileName"), ScalarBinaryOp.NotHasPrefix,
                new LiteralScalar("power", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "NOT regexp_matches(lower(FileName)");
        AssertSqlContains(sql, "(^|[^[:alnum:]])");
    }

    [TestMethod]
    [Description("summarize|sort: semantic assertions allow either canonical SELECT-first SQL or DuckDB FROM-first SQL")]
    public void SummarizeSort_DuckDbNativeMode_AllowsFromFirstSyntax()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 10_000, applyDefaultLimit: false);
        var node = new SortNode(
            new AggregateNode(
                new ScanNode("ProcessEvent"),
                Aggregates: [new ProjectionExpr("count_", new FunctionCall("count", []))],
                GroupBy: [new ColumnRef("DeviceName")]),
            [new SortExpr(new ColumnRef("count_"), SortDirection.Desc)]);

        var sql = emitter.Emit(node);
        var normalizedSql = NormSql(sql);

        Assert.Contains("golden.ProcessEvent", normalizedSql);
        Assert.Contains("DeviceName", normalizedSql);
        Assert.Contains("count(*) AS count_", normalizedSql);
        Assert.Contains("GROUP BY DeviceName", normalizedSql);
        Assert.Contains("ORDER BY count_ DESC", normalizedSql);
        Assert.DoesNotMatchRegex(@"SELECT\s+\*\s+FROM\s+__kql_stage_\d+", normalizedSql);
    }

    [TestMethod]
    [Description("summarize|sort in semantic mode collapses to terminal ordered aggregate without implicit limit")]
    public void SummarizeSort_OptimizedMode_CollapsesIntoOrderedAggregateSelect()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 10_000, applyDefaultLimit: false);
        var node = new SortNode(
            new AggregateNode(
                new ScanNode("ProcessEvent"),
                Aggregates: [new ProjectionExpr("count_", new FunctionCall("count", []))],
                GroupBy: [new ColumnRef("DeviceName")]),
            [new SortExpr(new ColumnRef("count_"), SortDirection.Desc)]);

        var sql = emitter.Emit(node);
        var norm = NormSql(sql);

        Assert.DoesNotContain("WITH", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatchRegex(@"__kql_stage_\d+", norm);
        Assert.DoesNotMatchRegex(@"SELECT\s+\*", norm);
        Assert.MatchesRegex(
            @"SELECT\s+DeviceName\s*,\s*count\(\*\)\s+AS\s+count_\s+FROM\s+golden\.ProcessEvent\s+GROUP\s+BY\s+DeviceName\s+ORDER\s+BY\s+count_\s+DESC\s*;?\s*$",
            norm);
        Assert.DoesNotContain("LIMIT 10000", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatchRegex(@"\bLIMIT\b", norm);
    }

    [TestMethod]
    [Description("DistinctNode emits SELECT DISTINCT")]
    public void Tabular_Distinct_EmitsDistinctKeyword()
    {
        var node = new DistinctNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("FileName", new ColumnRef("FileName")),
             new ProjectionExpr("DeviceName", new ColumnRef("DeviceName"))]);

        var sql = _emitter.Emit(node);
        AssertSqlContains(sql, "SELECT DISTINCT FileName, DeviceName FROM");
    }

    [TestMethod]
    [Description("Negative multi-component timespan signs every unit, not just the first")]
    public void Timespan_NegativeMultiComponent_SignsEachUnit()
    {
        var node = new FilterNode(
            new ScanNode("ProcessEvent"),
            new BinaryScalar(
                new ColumnRef("Timestamp"),
                ScalarBinaryOp.Gt,
                new LiteralScalar(TimeSpan.Parse("-00:01:30", CultureInfo.InvariantCulture), LiteralKind.Timespan)));

        var sql = _emitter.Emit(node);
        // -90s must keep both units negative; "INTERVAL '-1 minutes 30 seconds'"
        // would be -30s in DuckDB.
        AssertSqlContains(sql, "INTERVAL '-1 minutes -30 seconds'");
    }

    [TestMethod]
    [Description("where|extend collapses the pre-extend filter stage into the extend source when single-use")]
    public void WhereExtend_OptimizedMode_InlinesFilterIntoExtendStage()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 10_000, applyDefaultLimit: false);
        var node = new ExtendNode(
            new FilterNode(
                new ScanNode("ProcessEvent"),
                new BinaryScalar(
                    new FunctionCall("tolower", [new ColumnRef("FileName")]),
                    ScalarBinaryOp.Eq,
                    new FunctionCall("tolower", [new LiteralScalar("powershell.exe", LiteralKind.String)]))),
            [
                new ProjectionExpr(
                    "EncodedPayload",
                    new FunctionCall(
                        "extract",
                        [
                            new LiteralScalar(@"-enc\s+([^\s]+)", LiteralKind.String),
                            new LiteralScalar(1, LiteralKind.Int),
                            new ColumnRef("ProcessCommandLine")
                        ]))
            ]);

        var sql = emitter.Emit(node);
        var norm = NormSql(sql);

        Assert.DoesNotContain("WITH", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatchRegex(@"__kql_stage_\d+", norm);
        Assert.Contains("FROM (SELECT *,", norm, StringComparison.Ordinal);
        Assert.Contains("FROM golden.ProcessEvent WHERE (lower(FileName) = lower('powershell.exe'))", norm, StringComparison.Ordinal);
        Assert.Contains("regexp_extract(ProcessCommandLine, '-enc\\s+([^\\s]+)', CAST(1 AS INTEGER))", norm, StringComparison.Ordinal);
        Assert.Contains("AS EncodedPayload", norm, StringComparison.Ordinal);
    }

    [TestMethod]
    [Description("where|extend optimized single computed scope still preserves the default safety limit")]
    public void WhereExtend_OptimizedMode_PreservesDefaultLimitAndTelemetry()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 10_000, applyDefaultLimit: true);
        var node = new ExtendNode(
            new FilterNode(
                new ScanNode("ProcessEvent"),
                new BinaryScalar(
                    new FunctionCall("tolower", [new ColumnRef("FileName")]),
                    ScalarBinaryOp.Eq,
                    new FunctionCall("tolower", [new LiteralScalar("powershell.exe", LiteralKind.String)]))),
            [new ProjectionExpr("LowerCommandLine", new FunctionCall("tolower", [new ColumnRef("ProcessCommandLine")]))]);

        var sql = emitter.Emit(node);
        var norm = NormSql(sql);

        Assert.DoesNotContain("WITH", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatchRegex(@"__kql_stage_\d+", norm);
        Assert.Contains("FROM (SELECT *,", norm, StringComparison.Ordinal);
        Assert.MatchesRegex(@"\bLIMIT\s+10000\s*;?\s*$", norm);
        Assert.AreEqual(0, emitter.LastRunStats?.FinalCteCount);
    }

    [TestMethod]
    [Description("where|extend|project without take collapses the single computed scope without CTE names")]
    public void WhereExtendProject_OptimizedMode_RemovesSingleComputedScopeStage()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 10_000, applyDefaultLimit: false);
        var node = new ProjectNode(
            new ExtendNode(
                new FilterNode(
                    new ScanNode("ProcessEvent"),
                    new BinaryScalar(
                        new FunctionCall("tolower", [new ColumnRef("FileName")]),
                        ScalarBinaryOp.Eq,
                        new FunctionCall("tolower", [new LiteralScalar("powershell.exe", LiteralKind.String)]))),
                [
                    new ProjectionExpr(
                        "EncodedPayload",
                        new FunctionCall(
                            "extract",
                            [
                                new LiteralScalar(@"-enc\s+([^\s]+)", LiteralKind.String),
                                new LiteralScalar(1, LiteralKind.Int),
                                new ColumnRef("ProcessCommandLine")
                            ]))
                ]),
            [
                new ProjectionExpr("Timestamp", new ColumnRef("Timestamp")),
                new ProjectionExpr("EncodedPayload", new ColumnRef("EncodedPayload"))
            ]);

        var sql = emitter.Emit(node);
        var norm = NormSql(sql);

        Assert.DoesNotContain("WITH", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatchRegex(@"__kql_stage_\d+", norm);
        Assert.Contains("SELECT Timestamp, EncodedPayload FROM (SELECT *,", norm, StringComparison.Ordinal);
        Assert.Contains("FROM golden.ProcessEvent WHERE (lower(FileName) = lower('powershell.exe'))", norm, StringComparison.Ordinal);
        Assert.Contains("COALESCE(regexp_extract(ProcessCommandLine, '-enc\\s+([^\\s]+)', CAST(1 AS INTEGER)), '') AS EncodedPayload", norm, StringComparison.Ordinal);
    }

    [TestMethod]
    [Description("where|extend with multiple same-scope computed columns still collapses safely")]
    public void WhereExtendProjectTake_OptimizedMode_CollapsesMultipleSameScopeComputedColumns()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 10_000, applyDefaultLimit: false);
        var node = new LimitNode(
            new ProjectNode(
                new ExtendNode(
                    new FilterNode(
                        new ScanNode("ProcessEvent"),
                        new BinaryScalar(
                            new ColumnRef("FileName"),
                            ScalarBinaryOp.Eq,
                            new LiteralScalar("powershell.exe", LiteralKind.String))),
                    [
                        new ProjectionExpr("LowerName", new FunctionCall("tolower", [new ColumnRef("FileName")])),
                        new ProjectionExpr("CommandLength", new FunctionCall("strlen", [new ColumnRef("ProcessCommandLine")]))
                    ]),
                [
                    new ProjectionExpr("Timestamp", new ColumnRef("Timestamp")),
                    new ProjectionExpr("LowerName", new ColumnRef("LowerName")),
                    new ProjectionExpr("CommandLength", new ColumnRef("CommandLength"))
                ]),
            10);

        var sql = emitter.Emit(node);
        var norm = NormSql(sql);

        Assert.DoesNotContain("WITH", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatchRegex(@"__kql_stage_\d+", norm);
        Assert.Contains("SELECT Timestamp, LowerName, CommandLength FROM (SELECT *,", norm, StringComparison.Ordinal);
        Assert.Contains("lower(FileName) AS LowerName", norm, StringComparison.Ordinal);
        Assert.Contains("length(ProcessCommandLine) AS CommandLength", norm, StringComparison.Ordinal);
        Assert.MatchesRegex(@"\bLIMIT\s+10\s*;?\s*$", norm);
        Assert.AreEqual(0, emitter.LastRunStats?.FinalCteCount);
    }

    [TestMethod]
    [Description("where|extend|project keeps computed alias in projection while still preserving explicit limit")]
    public void WhereExtendProjectTake_OptimizedMode_PreservesComputedAliasAndLimit()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 10_000, applyDefaultLimit: false);
        var node = new LimitNode(
            new ProjectNode(
                new ExtendNode(
                    new FilterNode(
                        new ScanNode("ProcessEvent"),
                        new BinaryScalar(
                            new FunctionCall("tolower", [new ColumnRef("FileName")]),
                            ScalarBinaryOp.Eq,
                            new FunctionCall("tolower", [new LiteralScalar("powershell.exe", LiteralKind.String)]))),
                    [
                        new ProjectionExpr(
                            "EncodedPayload",
                            new FunctionCall(
                                "extract",
                                [
                                    new LiteralScalar(@"-enc\s+([^\s]+)", LiteralKind.String),
                                    new LiteralScalar(1, LiteralKind.Int),
                                    new ColumnRef("ProcessCommandLine")
                                ]))
                    ]),
                [
                    new ProjectionExpr("Timestamp", new ColumnRef("Timestamp")),
                    new ProjectionExpr("EncodedPayload", new ColumnRef("EncodedPayload"))
                ]),
            25);

        var sql = emitter.Emit(node);
        var norm = NormSql(sql);

        Assert.Contains("SELECT Timestamp, EncodedPayload FROM", norm, StringComparison.Ordinal);
        Assert.Contains("AS EncodedPayload", norm, StringComparison.Ordinal);
        Assert.DoesNotContain("WITH", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatchRegex(@"__kql_stage_\d+", norm);
        Assert.MatchesRegex(@"\bLIMIT\s+25\s*;?\s*$", norm);
        Assert.AreEqual(0, emitter.LastRunStats?.FinalCteCount);
    }

    [TestMethod]
    [Description("sort over where|extend does not inline the computed scope ahead of terminal ordering")]
    public void SortOverWhereExtend_OptimizedMode_DoesNotInlineComputedScopeBeforeOrder()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 10_000, applyDefaultLimit: true);
        var node = new SortNode(
            new ExtendNode(
                new FilterNode(
                    new ScanNode("ProcessEvent"),
                    new BinaryScalar(
                        new ColumnRef("FileName"),
                        ScalarBinaryOp.Eq,
                        new LiteralScalar("powershell.exe", LiteralKind.String))),
                [new ProjectionExpr("LowerCommandLine", new FunctionCall("tolower", [new ColumnRef("ProcessCommandLine")]))]),
            [new SortExpr(new ColumnRef("LowerCommandLine"), SortDirection.Asc)]);

        var sql = emitter.Emit(node);
        var norm = NormSql(sql);

        Assert.Contains("WITH", norm, StringComparison.OrdinalIgnoreCase);
        Assert.MatchesRegex(@"__kql_stage_\d+", norm);
        Assert.Contains("lower(ProcessCommandLine) AS LowerCommandLine", norm, StringComparison.Ordinal);
        Assert.Contains("ORDER BY LowerCommandLine ASC NULLS FIRST", norm, StringComparison.Ordinal);
        Assert.MatchesRegex(@"\bLIMIT\s+10000\s*;?\s*$", norm);
        Assert.AreEqual(1, emitter.LastRunStats?.FinalCteCount);
    }

    [TestMethod]
    [Description("chained extend remains staged because the computed scope source is another stage")]
    public void ChainedExtend_OptimizedMode_DoesNotInlineThroughStageSource()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 10_000, applyDefaultLimit: false);
        var node = new ProjectNode(
            new ExtendNode(
                new ExtendNode(
                    new ScanNode("ProcessEvent"),
                    [new ProjectionExpr("LowerName", new FunctionCall("tolower", [new ColumnRef("FileName")]))]),
                [new ProjectionExpr("NameLength", new FunctionCall("strlen", [new ColumnRef("LowerName")]))]),
            [new ProjectionExpr("NameLength", new ColumnRef("NameLength"))]);

        var sql = emitter.Emit(node);
        var norm = NormSql(sql);

        Assert.Contains("WITH", norm, StringComparison.OrdinalIgnoreCase);
        Assert.MatchesRegex(@"__kql_stage_\d+", norm);
        Assert.Contains("lower(FileName) AS LowerName", norm, StringComparison.Ordinal);
        Assert.Contains("length(LowerName) AS NameLength", norm, StringComparison.Ordinal);
        Assert.Contains("SELECT NameLength FROM", norm, StringComparison.Ordinal);
        Assert.IsNotNull(emitter.LastRunStats);
        Assert.IsGreaterThan(0, emitter.LastRunStats!.FinalCteCount);
    }

    [TestMethod]
    [Description("where|extend|where|extend|project|take keeps one computed-column scope and avoids per-operator staging")]
    public void WhereExtendWhereExtendProjectTake_OptimizedMode_UsesSingleComputedColumnScope()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 10_000, applyDefaultLimit: false);
        var node = new LimitNode(
            new ProjectNode(
                new ExtendNode(
                    new FilterNode(
                        new ExtendNode(
                            new FilterNode(
                                new ScanNode("ProcessEvent"),
                                new BinaryScalar(
                                    new FunctionCall("tolower", [new ColumnRef("FileName")]),
                                    ScalarBinaryOp.Eq,
                                    new FunctionCall("tolower", [new LiteralScalar("powershell.exe", LiteralKind.String)]))),
                            [
                                new ProjectionExpr(
                                    "EncodedPayload",
                                    new FunctionCall(
                                        "coalesce",
                                        [
                                            new FunctionCall(
                                                "extract",
                                                [
                                                    new LiteralScalar(@"-enc\s+([^\s]+)", LiteralKind.String),
                                                    new LiteralScalar(1, LiteralKind.Int),
                                                    new ColumnRef("ProcessCommandLine")
                                                ]),
                                            new LiteralScalar(string.Empty, LiteralKind.String)
                                        ]))
                            ]),
                        new FunctionCall("isnotempty", [new ColumnRef("EncodedPayload")])),
                    [
                        new ProjectionExpr("DecodedPayload", new FunctionCall("base64_decode_tostring", [new ColumnRef("EncodedPayload")]))
                    ]),
                [
                    new ProjectionExpr("Timestamp", new ColumnRef("Timestamp")),
                    new ProjectionExpr("DeviceName", new ColumnRef("DeviceName")),
                    new ProjectionExpr("AccountName", new ColumnRef("AccountName")),
                    new ProjectionExpr("ProcessCommandLine", new ColumnRef("ProcessCommandLine")),
                    new ProjectionExpr("DecodedPayload", new ColumnRef("DecodedPayload"))
                ]),
            25);

        var sql = emitter.Emit(node);
        var norm = NormSql(sql);

        Assert.DoesNotContain("WITH __kql_stage_", norm, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM (SELECT *,", norm, StringComparison.Ordinal);
        Assert.Contains(") AS s WHERE", norm, StringComparison.Ordinal);
        Assert.Contains("regexp_extract(ProcessCommandLine, '-enc\\s+([^\\s]+)', CAST(1 AS INTEGER))", norm, StringComparison.Ordinal);
        Assert.Contains("AS EncodedPayload", norm, StringComparison.Ordinal);
        Assert.Contains("CAST(from_base64(EncodedPayload) AS VARCHAR) AS DecodedPayload", norm, StringComparison.Ordinal);
        Assert.Contains("lower(FileName) = lower('powershell.exe')", norm, StringComparison.Ordinal);
        Assert.Contains("EncodedPayload IS NOT NULL", norm, StringComparison.Ordinal);
        Assert.MatchesRegex(@"EncodedPayload\s*(<>|!=)\s*''", norm);
        Assert.DoesNotContain("__kql_stage_2", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("__kql_stage_3", norm, StringComparison.OrdinalIgnoreCase);
        Assert.MatchesRegex(@"\bLIMIT\s+25\s*;?\s*$", norm);
    }

    [TestMethod]
    [Description("where|project over base scan collapses single-use filter CTE into one SELECT block")]
    public void WhereInProject_OptimizedMode_CollapsesSingleUseFilterCte()
    {
        var node = new ProjectNode(
            new FilterNode(
                new ScanNode("NetworkSession"),
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
        AssertSqlContains(sql, "FROM golden.NetworkSession WHERE (RemotePort IN (4444, 1337, 8888, 9999, 31337))");
        Assert.MatchesRegex(
            @"SELECT\s+Timestamp\s*,\s*DeviceName\s*,\s*LocalIP\s*,\s*RemoteIP\s*,\s*RemotePort\s*,\s*InitiatingProcessFileName\s+FROM\s+golden\.NetworkSession",
            norm);
    }

    [TestMethod]
    [Description("Default LIMIT is opt-in; semantic mode can emit no implicit limit")]
    public void WhereInProject_SemanticMode_DoesNotAddImplicitLimit()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 10_000, applyDefaultLimit: false);
        var node = new ProjectNode(
            new FilterNode(
                new ScanNode("NetworkSession"),
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
    [Description("where|project|take over base scan collapses into one SELECT/FROM/WHERE/LIMIT block")]
    public void WhereProjectTake_OptimizedMode_CollapsesIntoSingleSelect()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 10_000, applyDefaultLimit: false);
        var node = new LimitNode(
            new ProjectNode(
                new FilterNode(
                    new ScanNode("ProcessEvent"),
                    new BinaryScalar(
                        new ColumnRef("FileName"),
                        ScalarBinaryOp.Has,
                        new LiteralScalar("powershell", LiteralKind.String))),
                [
                    new ProjectionExpr("Timestamp", new ColumnRef("Timestamp")),
                    new ProjectionExpr("DeviceName", new ColumnRef("DeviceName")),
                    new ProjectionExpr("AccountName", new ColumnRef("AccountName")),
                    new ProjectionExpr("ProcessCommandLine", new ColumnRef("ProcessCommandLine"))
                ]),
            50);

        var sql = emitter.Emit(node);
        var norm = NormSql(sql);

        Assert.DoesNotContain("WITH", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatchRegex(@"__kql_stage_\d+", norm);
        Assert.DoesNotMatchRegex(@"SELECT\s+\*", norm);
        Assert.MatchesRegex(
            @"SELECT\s+Timestamp\s*,\s*DeviceName\s*,\s*AccountName\s*,\s*ProcessCommandLine\s+FROM\s+golden\.ProcessEvent",
            norm);
        Assert.Contains("regexp_matches(lower(FileName)", norm, StringComparison.Ordinal);
        Assert.Contains("powershell", norm, StringComparison.OrdinalIgnoreCase);
        Assert.MatchesRegex(@"\bLIMIT\s+50\s*;?\s*$", norm);
        Assert.DoesNotContain("LIMIT 10000", norm, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    [Description("where|summarize|where|sort in semantic mode collapses into WHERE/GROUP BY/HAVING/ORDER BY")]
    public void WhereSummarizeWhereSort_OptimizedMode_CollapsesIntoAggregateSelect()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 10_000, applyDefaultLimit: false);
        var node = new SortNode(
            new FilterNode(
                new AggregateNode(
                    new FilterNode(
                        new ScanNode("NetworkSession"),
                        new FunctionCall("isempty", [new ColumnRef("RemoteUrl")])),
                    Aggregates:
                    [
                        new ProjectionExpr("count_", new FunctionCall("count", [])),
                        new ProjectionExpr("dcount_", new FunctionCall("dcount", [new ColumnRef("RemotePort")]))
                    ],
                    GroupBy:
                    [
                        new ColumnRef("DeviceName"),
                        new ColumnRef("RemoteIP"),
                        new ColumnRef("InitiatingProcessFileName")
                    ]),
                new BinaryScalar(new ColumnRef("count_"), ScalarBinaryOp.Gt, new LiteralScalar(3, LiteralKind.Int))),
            [new SortExpr(new ColumnRef("count_"), SortDirection.Desc)]);

        var sql = emitter.Emit(node);
        var norm = NormSql(sql);

        Assert.DoesNotContain("WITH", norm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatchRegex(@"__kql_stage_\d+", norm);
        Assert.Contains("FROM golden.NetworkSession", norm);
        Assert.Contains("WHERE (RemoteUrl IS NULL OR RemoteUrl = '')", norm);
        Assert.Contains("GROUP BY DeviceName, RemoteIP, InitiatingProcessFileName", norm);
        Assert.Contains("HAVING (count(*) > 3)", norm);
        Assert.Contains("ORDER BY count_ DESC", norm);
        Assert.DoesNotMatchRegex(@"WHERE\s+.*count_\s*>\s*3", norm);
    }

    [TestMethod]
    [Description("where|where|project over base scan merges filters with AND and preserves OR predicate grouping")]
    public void WhereWhereProject_OptimizedMode_CollapsesAndPreservesGrouping()
    {
        var emitter = new DuckDbQueryEmitter(defaultLimit: 10_000, applyDefaultLimit: false);
        var node = new ProjectNode(
            new FilterNode(
                new FilterNode(
                    new ScanNode("NetworkSession"),
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
            @"SELECT\s+Timestamp\s*,\s*DeviceName\s*,\s*RemoteUrl\s*,\s*InitiatingProcessFileName\s*,\s*InitiatingProcessCommandLine\s+FROM\s+golden\.NetworkSession",
            norm);
        Assert.MatchesRegex(@"WHERE\s+\(+\s*RemotePort\s*=\s*53\s*\)+\s+AND\s+\(", norm);
        Assert.MatchesRegex(@"WHERE\s+\(+\s*RemotePort\s*=\s*53\s*\)+\s+AND\s+\(+.*powershell.*\s+OR\s+.*cmd.*\)+", norm);
    }

    private static void AssertSqlContains(string sql, string fragment)
    {
        var norm = NormSql(sql);
        var normFrag = NormSql(fragment);
        Assert.Contains(normFrag, norm,
            $"Expected SQL to contain '{normFrag}'\nActual: {norm}");
    }

    private static T? FindFirst<T>(RelNode node)
                                                                                                    where T : RelNode
    {
        if (node is T match)
        {
            return match;
        }

        return node switch
        {
            LimitNode n => FindFirst<T>(n.Input),
            SortNode n => FindFirst<T>(n.Input),
            ProjectNode n => FindFirst<T>(n.Input),
            FilterNode n => FindFirst<T>(n.Input),
            ExtendNode n => FindFirst<T>(n.Input),
            AggregateNode n => FindFirst<T>(n.Input),
            SampleNode n => FindFirst<T>(n.Input),
            JoinNode n => FindFirst<T>(n.Left) ?? FindFirst<T>(n.Right),
            LetBindingNode n => FindFirst<T>(n.Body),
            _ => null
        };
    }

    // ─── Timespan / datetime function emission ───────────────────────
    // ─── Helpers ────────────────────────────────────────────────────

    [GeneratedRegex(@"\s+")]
    private static partial Regex MyRegex();

    private static string NormSql(string s) =>
            MyRegex().Replace(s.Trim(), " ");

    [GeneratedRegex(@"AS \(SELECT \* FROM __kql_stage_\d+\)")]
    private static partial Regex PassthroughPattern();
}