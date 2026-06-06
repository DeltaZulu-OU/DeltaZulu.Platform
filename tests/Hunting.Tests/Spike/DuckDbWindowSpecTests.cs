namespace Hunting.Tests.Spike;

using DuckDB.NET.Data;
using Hunting.Data;

/// <summary>
/// <para>
/// Spec verification tests: confirm that every DuckDB window function used
/// as a KQL serialize/prev/next/row_number translation target works as
/// expected in DuckDB.NET.
/// </para>
/// <para>
/// Source: https://duckdb.org/docs/current/sql/functions/window_functions
///         https://duckdb.org/2025/05/02/stream-windowing-functions
/// </para>
/// </summary>
[TestClass]
public sealed class DuckDbWindowSpecTests
{
    private static DuckDBConnection _conn = null!;

    [ClassCleanup]
    public static void Cleanup() => _conn.Dispose();

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        _conn = new DuckDBConnection("DataSource=:memory:");
        _conn.Open();

        using var cmd = _conn.CreateCommand();

        // Create test table with ordered event data
        cmd.CommandText =
            """
            CREATE TABLE test_events (
                ts TIMESTAMP,
                device VARCHAR,
                event_type VARCHAR,
                value INTEGER
            );
            INSERT INTO test_events VALUES
                (TIMESTAMP '2024-01-01 10:00:00', 'A', 'login',  1),
                (TIMESTAMP '2024-01-01 10:01:00', 'A', 'login',  2),
                (TIMESTAMP '2024-01-01 10:05:00', 'A', 'login',  3),
                (TIMESTAMP '2024-01-01 10:06:00', 'B', 'login',  4),
                (TIMESTAMP '2024-01-01 10:10:00', 'A', 'logout', 5),
                (TIMESTAMP '2024-01-01 10:15:00', 'B', 'logout', 6),
                (TIMESTAMP '2024-01-01 10:20:00', 'A', 'login',  7),
                (TIMESTAMP '2024-01-01 10:30:00', 'A', 'login',  8)
            """;
        cmd.ExecuteNonQuery();
    }

    [TestMethod]
    [Description("Full beaconing pattern: lag + date_diff for inter-event gap detection")]
    public void BeaconingPattern_GapDetection()
    {
        var rows = Query(
            """
            WITH gaps AS (
                SELECT ts, device,
                       lag(ts) OVER (PARTITION BY device ORDER BY ts) AS prev_ts,
                       date_diff('second',
                           lag(ts) OVER (PARTITION BY device ORDER BY ts),
                           ts) AS gap_seconds
                FROM test_events
                WHERE device = 'A'
            )
            SELECT ts, gap_seconds
            FROM gaps
            WHERE gap_seconds IS NOT NULL
            ORDER BY ts
            """);

        Assert.IsNotEmpty(rows, "Should have rows with computed gaps");
        // First gap for device A: 10:00 → 10:01 = 60 seconds
        Assert.AreEqual(60L, AsInt64(rows[0][1]));
    }

    [TestMethod]
    [Description("sum() OVER (ORDER BY ts ROWS UNBOUNDED PRECEDING) produces cumulative sum")]
    public void CumulativeSum_Rows()
    {
        var rows = Query(
            """
            SELECT ts, value,
                   sum(value) OVER (ORDER BY ts ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS cum_sum
            FROM test_events
            ORDER BY ts
            """);

        // values are 1,2,3,4,5,6,7,8 → cumulative: 1,3,6,10,15,21,28,36
        Assert.AreEqual(1L, AsInt64(rows[0][2]));
        Assert.AreEqual(3L, AsInt64(rows[1][2]));
        Assert.AreEqual(6L, AsInt64(rows[2][2]));
        Assert.AreEqual(36L, AsInt64(rows[^1][2]));
    }

    [TestMethod]
    [Description("dense_rank() OVER (ORDER BY device) produces dense ranks")]
    public void DenseRank()
    {
        var rows = Query(
            """
            SELECT device,
                   dense_rank() OVER (ORDER BY device) AS dr
            FROM test_events
            ORDER BY device
            """);

        var rankA = AsInt64(rows.First(r => r[0]!.ToString() == "A")[1]);
        var rankB = AsInt64(rows.First(r => r[0]!.ToString() == "B")[1]);
        Assert.AreEqual(1L, rankA);
        Assert.AreEqual(2L, rankB);
    }

    [TestMethod]
    [Description("first_value gets first value in window")]
    public void FirstValue()
    {
        var rows = Query(
            """
            SELECT device, ts,
                   first_value(ts) OVER (PARTITION BY device ORDER BY ts) AS first_ts
            FROM test_events
            ORDER BY device, ts
            """);

        // All rows for device A should have the same first_ts
        var deviceA = rows.Where(r => r[0]!.ToString() == "A").ToList();
        var firstTs = deviceA[0][2];
        Assert.IsTrue(deviceA.All(r => r[2]!.Equals(firstTs)));
    }

    [TestMethod]
    [Description("lag(ts) OVER (ORDER BY ts) returns previous row's timestamp")]
    public void Lag_BasicOrdering()
    {
        var rows = Query(
            """
            SELECT ts,
                   lag(ts) OVER (ORDER BY ts) AS prev_ts
            FROM test_events
            ORDER BY ts
            """);

        Assert.HasCount(8, rows);
        Assert.IsNull(rows[0][1], "First row should have NULL prev_ts");
        Assert.IsNotNull(rows[1][1], "Second row should have non-NULL prev_ts");
    }

    // ─── lag() — KQL: prev() ────────────────────────────────────────
    [TestMethod]
    [Description("lag with PARTITION BY groups independently")]
    public void Lag_Partitioned()
    {
        var rows = Query(
            """
            SELECT device, ts,
                   lag(ts) OVER (PARTITION BY device ORDER BY ts) AS prev_ts
            FROM test_events
            ORDER BY device, ts
            """);

        // First row of each device partition should have NULL prev_ts
        var firstA = rows.First(r => r[0]!.ToString() == "A");
        Assert.IsNull(firstA[2], "First row of device A should have NULL prev_ts");

        var firstB = rows.First(r => r[0]!.ToString() == "B");
        Assert.IsNull(firstB[2], "First row of device B should have NULL prev_ts");
    }

    [TestMethod]
    [Description("lag with offset parameter (prev(x, 2) → lag(x, 2))")]
    public void Lag_WithOffset()
    {
        var rows = Query(
            """
            SELECT ts,
                   lag(ts, 2) OVER (ORDER BY ts) AS prev2_ts
            FROM test_events
            ORDER BY ts
            """);

        Assert.IsNull(rows[0][1], "Row 0 should have NULL");
        Assert.IsNull(rows[1][1], "Row 1 should have NULL");
        Assert.IsNotNull(rows[2][1], "Row 2 should have non-NULL prev2_ts");
    }

    [TestMethod]
    [Description("lead(ts) OVER (ORDER BY ts) returns next row's timestamp")]
    public void Lead_BasicOrdering()
    {
        var rows = Query(
            """
            SELECT ts,
                   lead(ts) OVER (ORDER BY ts) AS next_ts
            FROM test_events
            ORDER BY ts
            """);

        Assert.IsNotNull(rows[0][1], "First row should have non-NULL next_ts");
        Assert.IsNull(rows[^1][1], "Last row should have NULL next_ts");
    }

    [TestMethod]
    [Description("nth_value gets the nth value in window")]
    public void NthValue()
    {
        var rows = Query(
            """
            SELECT ts,
                   nth_value(ts, 3) OVER (ORDER BY ts ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING) AS third_ts
            FROM test_events
            ORDER BY ts
            """);

        // Third event is at 10:05
        Assert.Contains("10:05", rows[0][1]!.ToString()!);
    }

    [TestMethod]
    [Description("rank() OVER (ORDER BY device) produces standard ranks")]
    public void Rank()
    {
        var rows = Query(
            """
            SELECT device,
                   rank() OVER (ORDER BY device) AS r
            FROM test_events
            ORDER BY device, r
            """);

        // Device A has 6 rows, so first B should have rank 7
        var firstB = rows.First(r => r[0]!.ToString() == "B");
        Assert.AreEqual(7L, AsInt64(firstB[1]));
    }

    [TestMethod]
    [Description("row_number() with PARTITION BY restarts per partition")]
    public void RowNumber_Partitioned()
    {
        var rows = Query(
            """
            SELECT device, ts,
                   row_number() OVER (PARTITION BY device ORDER BY ts) AS rn
            FROM test_events
            ORDER BY device, ts
            """);

        // First row of each partition should have rn=1
        var firstA = rows.First(r => r[0]!.ToString() == "A");
        Assert.AreEqual(1L, AsInt64(firstA[2]));

        var firstB = rows.First(r => r[0]!.ToString() == "B");
        Assert.AreEqual(1L, AsInt64(firstB[2]));
    }

    [TestMethod]
    [Description("row_number() OVER (ORDER BY ts) produces sequential integers")]
    public void RowNumber_Sequential()
    {
        var rows = Query(
            """
            SELECT ts,
                   row_number() OVER (ORDER BY ts) AS rn
            FROM test_events
            ORDER BY ts
            """);

        for (var i = 0; i < rows.Count; i++)
        {
            Assert.AreEqual((long)(i + 1), AsInt64(rows[i][1]));
        }
    }

    [TestMethod]
    [Description("Session window: gap detection + cumulative new_session flag")]
    public void SessionWindow_Pattern()
    {
        var rows = Query(
            """
            WITH gaps AS (
                SELECT ts, device,
                       lag(ts) OVER (ORDER BY ts) AS prev_ts,
                       date_diff('minute', lag(ts) OVER (ORDER BY ts), ts) AS gap_minutes
                FROM test_events
            ),
            sessions AS (
                SELECT ts, device, gap_minutes,
                       CASE WHEN gap_minutes >= 10 OR gap_minutes IS NULL THEN 1 ELSE 0 END AS new_session,
                       sum(CASE WHEN gap_minutes >= 10 OR gap_minutes IS NULL THEN 1 ELSE 0 END)
                           OVER (ORDER BY ts ROWS UNBOUNDED PRECEDING) AS session_id
                FROM gaps
            )
            SELECT session_id, count(*) AS events, min(ts) AS session_start, max(ts) AS session_end
            FROM sessions
            GROUP BY session_id
            ORDER BY session_id
            """);

        Assert.IsGreaterThanOrEqualTo(2, rows.Count, "Should detect at least 2 sessions (gap at 10:20 and 10:30)");
    }

    [TestMethod]
    [Description("count(*) OVER (ORDER BY ts RANGE BETWEEN INTERVAL 5 MINUTE PRECEDING AND CURRENT ROW)")]
    public void SlidingWindow_Range()
    {
        var rows = Query(
            """
            SELECT ts,
                   count(*) OVER (
                       ORDER BY ts
                       RANGE BETWEEN INTERVAL '5 minutes' PRECEDING AND CURRENT ROW
                   ) AS events_in_window
            FROM test_events
            ORDER BY ts
            """);

        Assert.HasCount(8, rows);
        // First row: only itself in window
        Assert.AreEqual(1L, AsInt64(rows[0][1]));
        // Row at 10:01: 10:00 and 10:01 are within 5 min
        Assert.AreEqual(2L, AsInt64(rows[1][1]));
    }

    private static long AsInt64(object? v) => v is null
            ? throw new InvalidOperationException("Value is null")
            : v switch
            {
                long l => l,
                int i => i,
                short s => s,
                byte b => b,
                System.Numerics.BigInteger bi => (long)bi,
                decimal d => (long)d,
                double db => (long)db,
                float f => (long)f,
                string s => long.Parse(s),
                _ => Convert.ToInt64(v)
            };

    private List<object?[]> Query(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        var rows = new List<object?[]>();
        while (reader.Read())
        {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[i] = DuckDbValueReader.ReadValue(reader, i);
            }

            rows.Add(row);
        }
        return rows;
    }
}
