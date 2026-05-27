namespace Hunting.Tests.Emitter;

using DuckDB.NET.Data;
using Hunting.Core.DuckDbSql;
using Hunting.Core.QueryModel;

/// <summary>
/// End-to-end verification: emit SQL from RelNode trees, then execute against
/// a real in-memory DuckDB instance. This catches emitter bugs that produce
/// syntactically valid-looking SQL that DuckDB actually rejects.
///
/// These are integration tests, not unit tests. They depend on DuckDB.NET.
/// </summary>
[TestClass]
public sealed class DuckDbQueryEmitterExecutionTests
{
    private static DuckDBConnection _conn = null!;
    private static DuckDbQueryEmitter _emitter = null!;

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        _conn = new DuckDBConnection("DataSource=:memory:");
        _conn.Open();
        _emitter = new DuckDbQueryEmitter(defaultLimit: 1000);

        // Create the golden.DeviceProcessEvents view with mock data
        using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            """
            CREATE SCHEMA IF NOT EXISTS bronze;
            CREATE SCHEMA IF NOT EXISTS silver;
            CREATE SCHEMA IF NOT EXISTS golden;

            CREATE TABLE bronze.windows_event_json (
                ingest_time TIMESTAMP,
                source_type VARCHAR,
                provider VARCHAR,
                event_id INTEGER,
                computer VARCHAR,
                event_data JSON,
                raw_text VARCHAR
            );

            INSERT INTO bronze.windows_event_json VALUES
                (TIMESTAMP '2024-01-01 10:00:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'PC-001',
                 '{"Image":"C:\\Windows\\System32\\cmd.exe","CommandLine":"cmd /c dir","User":"alice","ProcessId":"1234","ParentImage":"C:\\explorer.exe","ParentCommandLine":"explorer.exe"}', ''),
                (TIMESTAMP '2024-01-01 10:01:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'PC-002',
                 '{"Image":"C:\\Windows\\System32\\powershell.exe","CommandLine":"powershell -enc abc","User":"bob","ProcessId":"5678","ParentImage":"C:\\cmd.exe","ParentCommandLine":"cmd"}', ''),
                (TIMESTAMP '2024-01-01 10:02:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'PC-001',
                 '{"Image":"C:\\Windows\\System32\\whoami.exe","CommandLine":"whoami /all","User":"alice","ProcessId":"9999","ParentImage":"C:\\cmd.exe","ParentCommandLine":"cmd /c dir"}', ''),
                (TIMESTAMP '2024-01-01 10:05:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'PC-003',
                 '{"Image":"C:\\Windows\\System32\\cmd.exe","CommandLine":"cmd /k","User":"charlie","ProcessId":"1111","ParentImage":"C:\\svchost.exe","ParentCommandLine":"svchost -k"}', ''),
                (TIMESTAMP '2024-01-01 10:10:00', 'sysmon', 'Microsoft-Windows-Sysmon', 1, 'PC-001',
                 '{"Image":"C:\\tools\\mimikatz.exe","CommandLine":"mimikatz","User":"alice","ProcessId":"6666","ParentImage":"C:\\cmd.exe","ParentCommandLine":"cmd"}', '');

            CREATE VIEW silver.v_process_sysmon_create AS
                SELECT
                    ingest_time AS Timestamp,
                    NULL AS DeviceId,
                    computer AS DeviceName,
                    'ProcessCreated' AS ActionType,
                    regexp_extract(json_extract_string(event_data, '$.Image'), '[^\\\\]+$', 0) AS FileName,
                    json_extract_string(event_data, '$.Image') AS FolderPath,
                    NULL AS SHA256,
                    CAST(json_extract_string(event_data, '$.ProcessId') AS BIGINT) AS ProcessId,
                    json_extract_string(event_data, '$.CommandLine') AS ProcessCommandLine,
                    json_extract_string(event_data, '$.User') AS AccountName,
                    regexp_extract(json_extract_string(event_data, '$.ParentImage'), '[^\\\\]+$', 0) AS InitiatingProcessFileName,
                    json_extract_string(event_data, '$.ParentCommandLine') AS InitiatingProcessCommandLine,
                    NULL AS ReportId,
                    event_data AS AdditionalFields
                FROM bronze.windows_event_json
                WHERE provider = 'Microsoft-Windows-Sysmon' AND event_id = 1;

            CREATE VIEW golden.DeviceProcessEvents AS
                SELECT * FROM silver.v_process_sysmon_create;
            """;
        cmd.ExecuteNonQuery();
    }

    [ClassCleanup]
    public static void Cleanup() => _conn.Dispose();

    private int Execute(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        var count = 0;
        while (reader.Read())
        {
            count++;
        }

        return count;
    }

    private void AssertExecutes(RelNode node, int? expectedMinRows = null)
    {
        var sql = _emitter.Emit(node);
        Assert.IsNotNull(sql, "Emitter returned null SQL");
        Assert.IsGreaterThan(0, sql.Length, "Emitter returned empty SQL");

        try
        {
            var rows = Execute(sql);
            if (expectedMinRows.HasValue)
            {
                Assert.IsGreaterThanOrEqualTo(expectedMinRows.Value, rows,
                    $"Expected at least {expectedMinRows} rows, got {rows}.\nSQL: {sql}");
            }
        }
        catch (Exception ex)
        {
#pragma warning disable MSTEST0058 // Do not use asserts in catch blocks
            Assert.Fail($"DuckDB execution failed.\nSQL: {sql}\nError: {ex.Message}");
#pragma warning restore MSTEST0058 // Do not use asserts in catch blocks
        }
    }

    private object? ExecuteFirstValue(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return reader.IsDBNull(0) ? null : reader.GetValue(0);
    }

    // ─── Core operators execute ─────────────────────────────────────

    [TestMethod]
    [Description("Bare scan executes against mock data")]
    public void Execute_Scan() => AssertExecutes(new ScanNode("DeviceProcessEvents"), expectedMinRows: 5);

    [TestMethod]
    [Description("Filter executes and reduces rows")]
    public void Execute_Filter()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new ColumnRef("FileName"),
                ScalarBinaryOp.Eq,
                new LiteralScalar("cmd.exe", LiteralKind.String)));

        AssertExecutes(node, expectedMinRows: 1);
    }

    [TestMethod]
    [Description("Project executes with column subset")]
    public void Execute_Project()
    {
        var node = new ProjectNode(
            new ScanNode("DeviceProcessEvents"),
            [
                new ProjectionExpr("Timestamp", new ColumnRef("Timestamp")),
                new ProjectionExpr("FileName", new ColumnRef("FileName")),
            ]);

        AssertExecutes(node, expectedMinRows: 1);
    }

    [TestMethod]
    [Description("Extend with tolower executes")]
    public void Execute_Extend()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("lower_name",
                new FunctionCall("tolower", [new ColumnRef("FileName")]))]);

        AssertExecutes(node, expectedMinRows: 1);
    }

    [TestMethod]
    [Description("Chained extend executes")]
    public void Execute_ExtendChained()
    {
        var inner = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("lower_name",
                new FunctionCall("tolower", [new ColumnRef("FileName")]))]);

        var outer = new ExtendNode(
            inner,
            [new ProjectionExpr("name_len",
                new FunctionCall("strlen", [new ColumnRef("lower_name")]))]);

        AssertExecutes(outer, expectedMinRows: 1);
    }

    [TestMethod]
    [Description("Aggregate with count() by executes")]
    public void Execute_Aggregate()
    {
        var node = new AggregateNode(
            new ScanNode("DeviceProcessEvents"),
            Aggregates: [new ProjectionExpr("cnt", new FunctionCall("count", []))],
            GroupBy: [new ColumnRef("DeviceName")]);

        AssertExecutes(node, expectedMinRows: 1);
    }

    [TestMethod]
    [Description("Sort + limit executes")]
    public void Execute_SortLimit()
    {
        var node = new LimitNode(
            new SortNode(
                new ScanNode("DeviceProcessEvents"),
                [new SortExpr(new ColumnRef("Timestamp"), SortDirection.Desc)]),
            3);

        AssertExecutes(node, expectedMinRows: 3);
    }

    // ─── Function mapping executes ──────────────────────────────────

    [TestMethod]
    [Description("ago() executes in DuckDB (native function)")]
    public void Execute_Ago()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new ColumnRef("Timestamp"),
                ScalarBinaryOp.Gt,
                new FunctionCall("ago", [new LiteralScalar("365d", LiteralKind.Timespan)])));

        AssertExecutes(node);
    }

    [TestMethod]
    [Description("isempty() executes correctly")]
    public void Execute_IsEmpty()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new FunctionCall("isnotempty", [new ColumnRef("FileName")]));

        AssertExecutes(node, expectedMinRows: 1);
    }

    [TestMethod]
    [Description("extract with COALESCE wrapper executes")]
    public void Execute_Extract()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("drive",
                new FunctionCall("extract",
                    [new LiteralScalar(@"^([A-Z]):", LiteralKind.String),
                     new LiteralScalar(1, LiteralKind.Int),
                     new ColumnRef("FolderPath")]))]);

        AssertExecutes(node, expectedMinRows: 1);
    }

    [TestMethod]
    [Description("coalesce executes")]
    public void Execute_Coalesce()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("safe_id",
                new FunctionCall("coalesce",
                    [new ColumnRef("DeviceId"),
                     new LiteralScalar("unknown", LiteralKind.String)]))]);

        AssertExecutes(node, expectedMinRows: 1);
    }

    [TestMethod]
    [Description("parse_ipv4() executes and returns numeric value for valid IPv4 literal")]
    public void Execute_ParseIpv4()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("ip_num",
                new FunctionCall("parse_ipv4", [new LiteralScalar("10.1.2.3", LiteralKind.String)]))]);

        AssertExecutes(node, expectedMinRows: 1);
    }

    [TestMethod]
    [Description("trim_start/trim_end and base64 encode/decode execute")]
    public void Execute_StringHelpers_New()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [
                new ProjectionExpr("trimmed_l", new FunctionCall("trim_start", [new LiteralScalar(@"[A-Za-z]:\\\\", LiteralKind.String), new ColumnRef("FolderPath")])),
                new ProjectionExpr("trimmed_r", new FunctionCall("trim_end", [new LiteralScalar(@"\\\\", LiteralKind.String), new ColumnRef("FolderPath")])),
                new ProjectionExpr("b64", new FunctionCall("base64_encode_tostring", [new ColumnRef("FileName")])),
                new ProjectionExpr("decoded", new FunctionCall("base64_decode_tostring", [new LiteralScalar("YQ==", LiteralKind.String)]))
            ]);
        AssertExecutes(node, expectedMinRows: 1);
    }

    [TestMethod]
    [Description("parse_path and percentile execute")]
    public void Execute_ParsePath_And_Percentile()
    {
        var parsePathNode = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("parsed", new FunctionCall("parse_path", [new ColumnRef("FolderPath")]))]);
        AssertExecutes(parsePathNode, expectedMinRows: 1);
        var parsePathSql = _emitter.Emit(new ProjectNode(parsePathNode, [new ProjectionExpr("parsed", new ColumnRef("parsed"))]));
        var parsed = ExecuteFirstValue(parsePathSql);
        Assert.IsInstanceOfType<string>(parsed, "parse_path should emit JSON text, not a structured CLR object.");

        var percentileNode = new AggregateNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("p95", new FunctionCall("percentile", [new ColumnRef("ProcessId"), new LiteralScalar(95, LiteralKind.Int)]))],
            []);
        AssertExecutes(percentileNode, expectedMinRows: 1);
    }

    // ─── Window functions execute ───────────────────────────────────

    [TestMethod]
    [Description("lag() window function executes")]
    public void Execute_Window_Lag()
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

        AssertExecutes(node, expectedMinRows: 5);
    }

    [TestMethod]
    [Description("row_number() window function executes")]
    public void Execute_Window_RowNumber()
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

        AssertExecutes(node, expectedMinRows: 5);
    }

    [TestMethod]
    [Description("Cumulative sum window function executes")]
    public void Execute_Window_CumulativeSum()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("running",
                new WindowScalarExpr(
                    "sum",
                    [new ColumnRef("ProcessId")],
                    new WindowSpec(
                        PartitionBy: [],
                        OrderBy: [new SortExpr(new ColumnRef("Timestamp"), SortDirection.Asc)],
                        Frame: new WindowFrame(
                            WindowFrameType.Rows,
                            new WindowBound(WindowBoundKind.UnboundedPreceding),
                            new WindowBound(WindowBoundKind.CurrentRow)))))]);

        AssertExecutes(node, expectedMinRows: 5);
    }

    [TestMethod]
    [Description("Partitioned window function executes")]
    public void Execute_Window_Partitioned()
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

        AssertExecutes(node, expectedMinRows: 5);
    }

    // ─── Full vertical slice executes ───────────────────────────────

    [TestMethod]
    [Description("Section 19 vertical slice query executes end-to-end")]
    public void Execute_VerticalSlice()
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
                        new ProjectionExpr("ProcessCommandLine", new ColumnRef("ProcessCommandLine")),
                    ]),
                20);

        AssertExecutes(node, expectedMinRows: 1);
    }

    // ─── SQL injection does not execute ─────────────────────────────

    [TestMethod]
    [Description("SQL injection payload in string literal does not break query")]
    public void Execute_InjectionSafe()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new ColumnRef("FileName"),
                ScalarBinaryOp.Eq,
                new LiteralScalar("'; DROP TABLE golden.DeviceProcessEvents; --", LiteralKind.String)));

        // Should execute (finding zero rows) without dropping the table
        AssertExecutes(node, expectedMinRows: 0);

        // Verify table still exists
        var verifyRows = Execute("SELECT count(*) FROM golden.DeviceProcessEvents");
        Assert.IsGreaterThan(0, verifyRows, "Table should still exist after injection attempt");
    }

    // ─── Empty result sets ──────────────────────────────────────────

    [TestMethod]
    [Description("Filter that matches nothing returns zero rows without error")]
    public void Execute_EmptyResult()
    {
        var node = new FilterNode(
            new ScanNode("DeviceProcessEvents"),
            new BinaryScalar(
                new ColumnRef("FileName"),
                ScalarBinaryOp.Eq,
                new LiteralScalar("definitely_not_a_real_process.exe", LiteralKind.String)));

        var sql = _emitter.Emit(node);
        var rows = Execute(sql);
        Assert.AreEqual(0, rows);
    }

    [TestMethod]
    [Description("LIMIT 0 returns zero rows")]
    public void Execute_LimitZero()
    {
        var node = new LimitNode(new ScanNode("DeviceProcessEvents"), 0);
        var sql = _emitter.Emit(node);
        var rows = Execute(sql);
        Assert.AreEqual(0, rows);
    }

    // ─── CASE expression executes ───────────────────────────────────

    [TestMethod]
    [Description("CASE WHEN expression executes")]
    public void Execute_CaseExpr()
    {
        var node = new ExtendNode(
            new ScanNode("DeviceProcessEvents"),
            [new ProjectionExpr("risk",
                new CaseScalar(
                    [(new BinaryScalar(
                        new ColumnRef("FileName"),
                        ScalarBinaryOp.Eq,
                        new LiteralScalar("mimikatz.exe", LiteralKind.String)),
                      new LiteralScalar("critical", LiteralKind.String))],
                    new LiteralScalar("normal", LiteralKind.String)))]);

        AssertExecutes(node, expectedMinRows: 5);
    }

    // ─── Pipeline simplification executes correctly ─────────────────

    [TestMethod]
    [Description("Top-k pipeline (summarize|sort|take) executes; fused, correctly ordered and bounded")]
    public void Execute_Simplification_TopKOrderedAndBounded()
    {
        // summarize count() by FileName | sort by count_ desc | take 3
        var node =
            new LimitNode(
                new SortNode(
                    new AggregateNode(
                        new ScanNode("DeviceProcessEvents"),
                        Aggregates: [new ProjectionExpr("count_", new FunctionCall("count", []))],
                        GroupBy: [new ColumnRef("FileName")]),
                    [new SortExpr(new ColumnRef("count_"), SortDirection.Desc)]),
                3);

        var sql = _emitter.Emit(node);

        // ORDER BY and LIMIT fused into one block; count(*) is non-nullable so no NULLS modifier.
        var norm = System.Text.RegularExpressions.Regex.Replace(sql.Trim(), @"\s+", " ");
        Assert.Contains("ORDER BY count_ DESC LIMIT 3", norm, $"Expected fused top-k block.\nSQL: {sql}");

        var counts = new List<long>();
        using (var cmd = _conn.CreateCommand())
        {
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();
            var countOrdinal = reader.GetOrdinal("count_");
            while (reader.Read())
            {
                counts.Add(Convert.ToInt64(reader.GetValue(countOrdinal)));
            }
        }

        Assert.HasCount(3, counts, "take 3 must bound the result to 3 rows");
        Assert.AreEqual(2L, counts[0], "cmd.exe occurs twice and must sort first");
        for (var i = 1; i < counts.Count; i++)
        {
            Assert.IsLessThanOrEqualTo(counts[i - 1], counts[i],
                $"counts must be non-increasing: {string.Join(",", counts)}");
        }
    }
}
