using DeltaZulu.Platform.Data.DuckDb;
using DuckDB.NET.Data;

namespace DeltaZulu.Platform.Tests.Analytics;

[TestClass]
public sealed class DuckDbValueReaderTests
{
    [TestMethod]
    [DataRow("0.000000000000000000000000000001", 30)]
    [DataRow("79228162514264337593543950336", 0)]
    [DataRow("-79228162514264337593543950336", 0)]
    [DataRow("0.00000000000000000000000000001", 29)]
    public void ReadValue_RejectsUnrepresentableDecimal(string value, int scale)
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT CAST('{value}' AS DECIMAL(38,{scale}))";
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        Assert.ThrowsExactly<OverflowException>(() => DuckDbValueReader.ReadValue(reader, 0));
    }

    [TestMethod]
    public void ReadValue_PreservesExactDecimalAndSignedInterval()
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1.000000000000000000000000000000::DECIMAL(38,30), INTERVAL '-1 second', NULL::DECIMAL(38,30)";
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(1m, DuckDbValueReader.ReadValue(reader, 0));
        Assert.AreEqual(TimeSpan.FromSeconds(-1), DuckDbValueReader.ReadValue(reader, 1));
        Assert.IsNull(DuckDbValueReader.ReadValue(reader, 2));
    }

    [TestMethod]
    [DataRow("79228162514264337593543950335", 0)]
    [DataRow("-79228162514264337593543950335", 0)]
    [DataRow("0.0000000000000000000000000001", 28)]
    [DataRow("-0.0000000000000000000000000001", 28)]
    [DataRow("0.000000000000000000000000000000", 30)]
    public void ReadValue_PreservesDecimalBoundaries(string value, int scale)
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT CAST('{value}' AS DECIMAL(38,{scale}))";
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
            DuckDbValueReader.ReadValue(reader, 0));
    }

    [TestMethod]
    public void ReadValue_PreservesGuidAndTimestampWithTimeZone()
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT '01234567-89ab-cdef-0123-456789abcdef'::UUID, TIMESTAMPTZ '2026-01-01 02:00:00.123456+02:00', TIMESTAMP '2026-01-01 02:00:00.123456', NULL::UUID";
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), DuckDbValueReader.ReadValue(reader, 0));
        var instant = (DateTime)DuckDbValueReader.ReadValue(reader, 1)!;
        Assert.AreEqual(DateTimeKind.Utc, instant.Kind);
        Assert.AreEqual(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(1234560), instant);
        var wallClock = (DateTime)DuckDbValueReader.ReadValue(reader, 2)!;
        Assert.AreEqual(DateTimeKind.Unspecified, wallClock.Kind);
        Assert.AreEqual(new DateTime(2026, 1, 1, 2, 0, 0).AddTicks(1234560), wallClock);
        Assert.IsNull(DuckDbValueReader.ReadValue(reader, 3));
    }

    [TestMethod]
    [DataRow("1 month")]
    [DataRow("-1 month")]
    [DataRow("20000000 days")]
    [DataRow("-20000000 days")]
    public void ReadValue_RejectsCalendarOrOutOfRangeIntervals(string interval)
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT INTERVAL '{interval}'";
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => DuckDbValueReader.ReadValue(reader, 0));
    }

    [TestMethod]
    [DataRow("1 day -1 microsecond", 863999999990L)]
    [DataRow("-1 day 1 microsecond", -863999999990L)]
    [DataRow("1 microsecond", 10L)]
    [DataRow("-1 microsecond", -10L)]
    public void ReadValue_PreservesMixedSignIntervals(string interval, long ticks)
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT INTERVAL '{interval}'";
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(TimeSpan.FromTicks(ticks), DuckDbValueReader.ReadValue(reader, 0));
    }

    [TestMethod]
    public void ReadValue_PreservesPrimitiveTypes()
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 42::INTEGER AS NumberValue, 'abc' AS TextValue";

        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());

        Assert.AreEqual(42, DuckDbValueReader.ReadValue(reader, 0));
        Assert.AreEqual("abc", DuckDbValueReader.ReadValue(reader, 1));
    }

    [TestMethod]
    public void ReadValue_NormalizesJsonAndNestedValuesToSerializableStrings()
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                '{"x":1}'::JSON AS JsonValue,
                to_json({'a': 1, 'b': 'x'}) AS StructValue,
                to_json([1, 2, 3]) AS ListValue;
            """;

        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());

        Assert.IsInstanceOfType<string>(DuckDbValueReader.ReadValue(reader, 0));
        Assert.AreEqual("{\"a\":1,\"b\":\"x\"}", DuckDbValueReader.ReadValue(reader, 1, "STRUCT(a INTEGER, b VARCHAR)"));
        Assert.AreEqual("[1,2,3]", DuckDbValueReader.ReadValue(reader, 2, "INTEGER[]"));
    }

    [TestMethod]
    public void ReadValue_RejectsUnprojectedNestedValues()
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT {'x': 1}, [1, 2]";
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        Assert.ThrowsExactly<NotSupportedException>(() => DuckDbValueReader.ReadValue(reader, 0));
        Assert.ThrowsExactly<NotSupportedException>(() => DuckDbValueReader.ReadValue(reader, 1));
    }

    [TestMethod]
    [DataRow("127.0.0.1")]
    [DataRow("0.0.0.0/0")]
    [DataRow("255.255.255.255/31")]
    [DataRow("::")]
    [DataRow("::/0")]
    [DataRow("2001:db8::1/64")]
    [DataRow("7fff:ffff:ffff:ffff:ffff:ffff:ffff:ffff")]
    [DataRow("8000::")]
    [DataRow("ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff/127")]
    public void ReadValue_NormalizesInetExtensionValuesToStrings(string address)
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();

        using var install = connection.CreateCommand();
        install.CommandText = "INSTALL inet; LOAD inet;";
        install.ExecuteNonQuery();

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT CAST('{address}'::INET AS VARCHAR) AS IpAddress, CAST(NULL::INET AS VARCHAR)";

        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());

        var value = DuckDbValueReader.ReadValue(reader, 0, "INET");
        Assert.AreEqual(address, value);
        Assert.IsNull(DuckDbValueReader.ReadValue(reader, 1, "INET"));
    }
}