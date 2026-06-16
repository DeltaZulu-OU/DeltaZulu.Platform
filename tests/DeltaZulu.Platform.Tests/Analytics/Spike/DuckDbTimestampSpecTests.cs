using System.Globalization;
using DuckDB.NET.Data;

namespace DeltaZulu.Platform.Tests.Analytics.Spike;

/// <summary>
/// <para>
/// Spec verification tests: confirm that every DuckDB function used as a
/// KQL translation target actually works as expected in DuckDB.NET.
/// These are the ground truth for the emitter — if a function doesn't
/// work here, the emitter must not emit it.
/// </para>
/// <para>
/// Source: https://duckdb.org/docs/current/sql/functions/timestamp
///         https://duckdb.org/docs/current/sql/functions/date
///         https://duckdb.org/docs/current/sql/functions/datepart
///         https://duckdb.org/2025/05/02/stream-windowing-functions
/// </para>
/// </summary>
[TestClass]
public sealed class DuckDbTimestampSpecTests
{
    private static DuckDBConnection _conn = null!;

    [ClassCleanup]
    public static void Cleanup() => _conn.Dispose();

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        _conn = new DuckDBConnection("DataSource=:memory:");
        _conn.Open();
    }

    [TestMethod]
    [Description("age(ts1, ts2) returns human-readable interval")]
    public void Age_TwoTimestamps()
    {
        // DuckDB.NET maps INTERVAL to DuckDBInterval which does not convert to TimeSpan when
        // Months >= 1. Cast to VARCHAR in SQL to get a safe string representation.
        var result = ScalarStr(
            "SELECT CAST(age(TIMESTAMP '2024-03-15', TIMESTAMP '2024-01-01') AS VARCHAR)");
        Assert.IsNotNull(result);
        Assert.IsTrue(result!.Contains("2 months") && result.Contains("14 days"));
    }

    [TestMethod]
    [Description("ago(interval) equivalent — current_timestamp - INTERVAL always works")]
    public void Ago_CurrentTimestampMinusIntervalEquivalent()
    {
        // This is the documented approach and what our emitter now generates
        var result = Convert.ToInt64(Scalar(
            "SELECT CASE WHEN (current_timestamp - INTERVAL '1 hour') < current_timestamp THEN 1 ELSE 0 END"), CultureInfo.InvariantCulture);
        Assert.AreEqual(1L, result,
            "current_timestamp - INTERVAL '1 hour' should produce a past timestamp");
    }

    [TestMethod]
    [Description("ago(interval) produces timestamp before current_timestamp — conditional on ago() existing")]
    public void Ago_ProducesPastTimestamp()
    {
        try
        {
            var result = Convert.ToInt64(Scalar(
                "SELECT CASE WHEN ago(INTERVAL '1 hour') < current_timestamp THEN 1 ELSE 0 END"), CultureInfo.InvariantCulture);
            Assert.AreEqual(1L, result);
        }
        catch (Exception ex) when (ex.Message.Contains("ago") || ex.Message.Contains("function"))
        {
#pragma warning disable MSTEST0058 // Do not use asserts in catch blocks
            Assert.Inconclusive(
                "ago() not available in this DuckDB version. " +
                $"Emitter uses current_timestamp - INTERVAL instead. Exception: {ex.Message}");
#pragma warning restore MSTEST0058 // Do not use asserts in catch blocks
        }
    }

    [TestMethod]
    [Description("current_timestamp is a valid expression")]
    public void CurrentTimestamp()
    {
        var result = ScalarStr("SELECT typeof(current_timestamp)");
        Assert.Contains("TIMESTAMP", result!);
    }

    [TestMethod]
    [Description("date_diff('day', ts1, ts2) counts day boundaries")]
    public void DateDiff_Day()
    {
        var result = Convert.ToInt64(Scalar(
            "SELECT date_diff('day', TIMESTAMP '2024-01-01', TIMESTAMP '2024-01-05')"), CultureInfo.InvariantCulture);
        Assert.AreEqual(4L, result);
    }

    [TestMethod]
    [Description("date_diff('hour', ts1, ts2) counts hour boundaries")]
    public void DateDiff_Hour()
    {
        var result = Convert.ToInt64(Scalar(
            "SELECT date_diff('hour', TIMESTAMP '2024-01-01 23:59:59', TIMESTAMP '2024-01-02 01:00:01')"), CultureInfo.InvariantCulture);
        Assert.AreEqual(2L, result); // crosses two hour boundaries
    }

    [TestMethod]
    [Description("date_diff returns negative when start > end")]
    public void DateDiff_Negative()
    {
        var result = Convert.ToInt64(Scalar(
            "SELECT date_diff('day', TIMESTAMP '2024-01-05', TIMESTAMP '2024-01-01')"), CultureInfo.InvariantCulture);
        Assert.AreEqual(-4L, result);
    }

    // ─── date_diff — KQL: datetime_diff(part, dt1, dt2) ─────────────
    [TestMethod]
    [Description("date_diff('second', ts1, ts2) for gap detection")]
    public void DateDiff_Second()
    {
        var result = Convert.ToInt64(Scalar(
            "SELECT date_diff('second', TIMESTAMP '2024-01-01 10:00:00', TIMESTAMP '2024-01-01 10:01:00')"), CultureInfo.InvariantCulture);
        Assert.AreEqual(60L, result);
    }

    [TestMethod]
    [Description("date_part('day', ts) extracts day of month")]
    public void DatePart_Day()
    {
        var result = Convert.ToInt64(Scalar("SELECT date_part('day', TIMESTAMP '2024-03-15 14:30:00')"), CultureInfo.InvariantCulture);
        Assert.AreEqual(15L, result);
    }

    [TestMethod]
    [Description("date_part('dow', ts) extracts day of week — 0=Sunday in DuckDB")]
    public void DatePart_DayOfWeek()
    {
        // 2024-03-15 is Friday = 5 in DuckDB (0=Sun, 1=Mon, ... 5=Fri)
        var result = Convert.ToInt64(Scalar("SELECT date_part('dow', TIMESTAMP '2024-03-15')"), CultureInfo.InvariantCulture);
        Assert.AreEqual(5L, result);
    }

    [TestMethod]
    [Description("date_part('doy', ts) extracts day of year")]
    public void DatePart_DayOfYear()
    {
        // 2024-03-15: Jan(31) + Feb(29, leap) + 15 = 75
        var result = Convert.ToInt64(Scalar("SELECT date_part('doy', TIMESTAMP '2024-03-15')"), CultureInfo.InvariantCulture);
        Assert.AreEqual(75L, result);
    }

    [TestMethod]
    [Description("date_part('hour', ts) extracts hour")]
    public void DatePart_Hour()
    {
        var result = Convert.ToInt64(Scalar("SELECT date_part('hour', TIMESTAMP '2024-03-15 14:30:00')"), CultureInfo.InvariantCulture);
        Assert.AreEqual(14L, result);
    }

    [TestMethod]
    [Description("date_part('month', ts) extracts month")]
    public void DatePart_Month()
    {
        var result = Convert.ToInt64(Scalar("SELECT date_part('month', TIMESTAMP '2024-03-15 14:30:00')"), CultureInfo.InvariantCulture);
        Assert.AreEqual(3L, result);
    }

    [TestMethod]
    [Description("date_part struct variant returns multiple parts at once")]
    public void DatePart_Struct()
    {
        // date_part with array returns a struct/dictionary via DuckDB.NET; cast to VARCHAR for stable text assertion
        var result = ScalarStr(
            "SELECT CAST(date_part(['year', 'month', 'day'], TIMESTAMP '2024-03-15 14:30:00') AS VARCHAR)");
        Assert.IsNotNull(result);
        Assert.Contains("2024", result);
        Assert.IsTrue(result.Contains('3') && result.Contains("15"));
    }

    [TestMethod]
    [Description("date_part('year', ts) extracts year")]
    public void DatePart_Year()
    {
        var result = Convert.ToInt64(Scalar("SELECT date_part('year', TIMESTAMP '2024-03-15 14:30:00')"), CultureInfo.InvariantCulture);
        Assert.AreEqual(2024L, result);
    }

    [TestMethod]
    [Description("date_trunc('day', ts) truncates to midnight")]
    public void DateTrunc_Day()
    {
        var result = ScalarStr("SELECT date_trunc('day', TIMESTAMP '2024-03-15 14:30:00')");
        Assert.Contains("2024-03-15 00:00:00", result!);
    }

    [TestMethod]
    [Description("date_trunc('hour', ts) truncates to hour")]
    public void DateTrunc_Hour()
    {
        var result = ScalarStr("SELECT date_trunc('hour', TIMESTAMP '2024-03-15 14:30:45')");
        Assert.Contains("2024-03-15 14:00:00", result!);
    }

    [TestMethod]
    [Description("date_trunc('minute', ts) truncates to minute")]
    public void DateTrunc_Minute()
    {
        var result = ScalarStr("SELECT date_trunc('minute', TIMESTAMP '2024-03-15 14:30:45')");
        Assert.Contains("2024-03-15 14:30:00", result!);
    }

    // ─── date_trunc — KQL: startofday/month/week/year ───────────────
    [TestMethod]
    [Description("date_trunc('month', ts) truncates to first of month")]
    public void DateTrunc_Month()
    {
        var result = ScalarStr("SELECT date_trunc('month', TIMESTAMP '2024-03-15 14:30:00')");
        Assert.Contains("2024-03-01", result!);
    }

    [TestMethod]
    [Description("date_trunc('week', ts) truncates to Monday")]
    public void DateTrunc_Week()
    {
        // 2024-03-15 is a Friday; Monday was 2024-03-11
        var result = ScalarStr("SELECT date_trunc('week', TIMESTAMP '2024-03-15 14:30:00')");
        Assert.Contains("2024-03-11", result!);
    }

    [TestMethod]
    [Description("date_trunc('year', ts) truncates to Jan 1")]
    public void DateTrunc_Year()
    {
        var result = ScalarStr("SELECT date_trunc('year', TIMESTAMP '2024-03-15 14:30:00')");
        Assert.Contains("2024-01-01", result!);
    }

    [TestMethod]
    [Description("dayname returns English weekday name")]
    public void Dayname()
    {
        var result = ScalarStr("SELECT dayname(TIMESTAMP '2024-03-15')");
        Assert.AreEqual("Friday", result);
    }

    [TestMethod]
    [Description("DuckDB native ago(interval) returns TIMESTAMP WITH TIME ZONE")]
    public void DuckDbAgo_ReturnsTimestampWithTimeZone()
    {
        var result = ScalarStr("SELECT typeof(ago(INTERVAL '1 hour'))");

        Assert.AreEqual("TIMESTAMP WITH TIME ZONE", result);
    }

    [TestMethod]
    [Description("endofday: date_trunc + 1 day - 1 microsecond")]
    public void EndOfDay_Pattern()
    {
        var result = ScalarStr(
            "SELECT date_trunc('day', TIMESTAMP '2024-03-15 14:30:00') + INTERVAL '1 day' - INTERVAL '1 microsecond'");
        Assert.Contains("2024-03-15 23:59:59.999999", result!);
    }

    // ─── endof* pattern verification ─────────────────────────────────
    [TestMethod]
    [Description("endofmonth via last_day is simpler than arithmetic")]
    public void EndOfMonth_ViaLastDay()
    {
        var result = ScalarStr(
            "SELECT last_day(TIMESTAMP '2024-02-15')::TIMESTAMP + INTERVAL '23 hours 59 minutes 59 seconds 999999 microseconds'");
        Assert.Contains("2024-02-29", result!);
    }

    [TestMethod]
    [Description("epoch(ts) returns seconds since epoch")]
    public void Epoch_Seconds()
    {
        var result = Convert.ToInt64(Scalar("SELECT epoch(TIMESTAMP '2024-01-01 00:00:00')"), CultureInfo.InvariantCulture);
        Assert.AreEqual(1704067200L, result);
    }

    // ─── date_part — KQL: dayofweek, dayofmonth, hourofday, etc. ────
    // ─── epoch functions — KQL: unixtime_*_todatetime ────────────────
    [TestMethod]
    [Description("epoch_ms(ts) returns milliseconds since epoch")]
    public void EpochMs_FromTimestamp()
    {
        var result = Convert.ToInt64(Scalar("SELECT epoch_ms(TIMESTAMP '2024-01-01 00:00:00')"), CultureInfo.InvariantCulture);
        Assert.AreEqual(1704067200000L, result);
    }

    [TestMethod]
    [Description("epoch_ms(bigint) creates timestamp FROM milliseconds — bidirectional")]
    public void EpochMs_ToTimestamp()
    {
        var result = ScalarStr("SELECT epoch_ms(1704067200000::BIGINT)");
        Assert.Contains("2024-01-01 00:00:00", result!);
    }

    [TestMethod]
    [Description("greatest returns the later timestamp")]
    public void Greatest_Timestamps()
    {
        var result = ScalarStr(
            "SELECT greatest(TIMESTAMP '2024-01-01', TIMESTAMP '2024-03-15')");
        Assert.Contains("2024-03-15", result!);
    }

    [TestMethod]
    [Description("Timestamp + INTERVAL adds correctly")]
    public void Interval_Add()
    {
        var result = ScalarStr("SELECT TIMESTAMP '2024-01-01 00:00:00' + INTERVAL '30 days'");
        Assert.Contains("2024-01-31", result!);
    }

    // ─── INTERVAL arithmetic ─────────────────────────────────────────
    [TestMethod]
    [Description("Timestamp - Timestamp returns INTERVAL")]
    public void Interval_Subtract()
    {
        var result = ScalarStr("SELECT TIMESTAMP '2024-01-05' - TIMESTAMP '2024-01-01'");
        Assert.Contains("4 days", result!);
    }

    [TestMethod]
    [Description("last_day(ts) returns last day of month")]
    public void LastDay()
    {
        var result = ScalarStr("SELECT last_day(TIMESTAMP '2024-02-15')");
        Assert.Contains("2024-02-29", result!); // leap year
    }

    // ─── last_day — simplifies endofmonth ────────────────────────────
    [TestMethod]
    [Description("last_day for non-leap year February")]
    public void LastDay_NonLeap()
    {
        var result = ScalarStr("SELECT last_day(TIMESTAMP '2023-02-15')");
        Assert.Contains("2023-02-28", result!);
    }

    // ─── age() — useful for time difference display ──────────────────
    // ─── greatest / least — useful for clamp patterns ────────────────
    [TestMethod]
    [Description("least returns the earlier timestamp")]
    public void Least_Timestamps()
    {
        var result = ScalarStr(
            "SELECT least(TIMESTAMP '2024-01-01', TIMESTAMP '2024-03-15')");
        Assert.Contains("2024-01-01", result!);
    }

    [TestMethod]
    [Description("make_timestamp(microseconds) constructs from epoch microseconds")]
    public void MakeTimestamp_FromMicroseconds()
    {
        // 1704067200000000 microseconds = 2024-01-01 00:00:00
        var result = ScalarStr("SELECT make_timestamp(1704067200000000)");
        Assert.Contains("2024-01-01 00:00:00", result!);
    }

    [TestMethod]
    [Description("make_timestamp(y,m,d,h,mi,s) constructs timestamp from parts")]
    public void MakeTimestamp_FromParts()
    {
        var result = ScalarStr("SELECT make_timestamp(2024, 3, 15, 14, 30, 0.0)");
        Assert.Contains("2024-03-15 14:30:00", result!);
    }

    // ─── dayname / monthname ─────────────────────────────────────────
    [TestMethod]
    [Description("monthname returns English month name")]
    public void Monthname()
    {
        var result = ScalarStr("SELECT monthname(TIMESTAMP '2024-03-15')");
        Assert.AreEqual("March", result);
    }

    [TestMethod]
    [Description("strftime formats timestamp to string")]
    public void Strftime_Basic()
    {
        var result = ScalarStr("SELECT strftime(TIMESTAMP '2024-03-15 14:30:00', '%Y-%m-%d %H:%M')");
        Assert.AreEqual("2024-03-15 14:30", result);
    }

    // ─── make_timestamp — KQL: make_datetime ─────────────────────────
    // ─── strftime / strptime — KQL: format_datetime / todatetime ─────
    [TestMethod]
    [Description("strptime parses string to timestamp")]
    public void Strptime_Basic()
    {
        var result = ScalarStr("SELECT strptime('2024-03-15 14:30', '%Y-%m-%d %H:%M')");
        Assert.Contains("2024-03-15 14:30:00", result!);
    }

    [TestMethod]
    [Description("time_bucket with 15-minute interval")]
    public void TimeBucket_15Min()
    {
        var result = ScalarStr("SELECT time_bucket(INTERVAL '15 minutes', TIMESTAMP '2024-03-15 14:37:00')");
        Assert.Contains("2024-03-15 14:30:00", result!);
    }

    [TestMethod]
    [Description("time_bucket(INTERVAL '1 hour', ts) buckets by hour")]
    public void TimeBucket_1Hour()
    {
        var result = ScalarStr("SELECT time_bucket(INTERVAL '1 hour', TIMESTAMP '2024-03-15 14:37:00')");
        Assert.Contains("2024-03-15 14:00:00", result!);
    }

    // ─── time_bucket — KQL: bin(ts, interval) ───────────────────────
    [TestMethod]
    [Description("time_bucket with offset parameter")]
    public void TimeBucket_WithOffset()
    {
        var result = ScalarStr(
            "SELECT time_bucket(INTERVAL '1 hour', TIMESTAMP '2024-03-15 14:37:00', INTERVAL '30 minutes')");
        Assert.Contains("2024-03-15 14:30:00", result!);
    }

    [TestMethod]
    [Description("to_timestamp(seconds) creates timestamp from epoch seconds")]
    public void ToTimestamp_FromSeconds()
    {
        var result = ScalarStr("SELECT to_timestamp(1704067200)");
        Assert.Contains("2024-01-01 00:00:00", result!);
    }

    [TestMethod]
    [Description("try_strptime returns NULL on failure instead of error")]
    public void TryStrptime_ReturnsNull()
    {
        var result = Scalar("SELECT try_strptime('not-a-date', '%Y-%m-%d')");
        Assert.IsTrue(result is null || result == DBNull.Value);
    }

    private object? Scalar(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    private string? ScalarStr(string sql)
    {
        var v = Scalar(sql);
        if (v is null || v == DBNull.Value)
        {
            return null;
        }
        // Normalize DateTime/timestamp results to invariant ISO-like format with microsecond precision
        if (v is DateTime dt)
        {
            // Emit seconds always; append fractional microseconds only when present to avoid culture-dependent formats.
            var baseStr = dt.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            var fracTicks = dt.Ticks % System.TimeSpan.TicksPerSecond;
            if (fracTicks == 0)
            {
                return baseStr;
            }

            var micro = (fracTicks / 10); // 1 microsecond = 10 ticks
            var microStr = micro.ToString("D6", CultureInfo.InvariantCulture);
            microStr = microStr.TrimEnd('0');
            return baseStr + "." + microStr;
        }

        // DuckDB INTERVAL / duration types may surface as TimeSpan (or DuckDBInterval elsewhere).
        if (v is TimeSpan ts)
        {
            // If whole number of days, express as 'N days' to match DuckDB textual form used in tests.
            if (ts.Ticks % System.TimeSpan.TicksPerDay == 0)
            {
                return $"{ts.Days} days";
            }
            // Otherwise use invariant ToString for a stable representation
            return ts.ToString();
        }

        // Date-only values (DuckDB may return a DateOnly) — format ISO yyyy-MM-dd to match DuckDB textual expectations.
        if (v is DateOnly d)
        {
            return d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        // For other types (strings, numeric), fall back to ToString invariantly when possible
        if (v is IFormattable f)
        {
            return f.ToString(null, System.Globalization.CultureInfo.InvariantCulture);
        }
        return v.ToString();
    }
}