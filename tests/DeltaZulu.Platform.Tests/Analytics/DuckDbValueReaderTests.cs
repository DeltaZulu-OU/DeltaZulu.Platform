using DeltaZulu.Platform.Data.DuckDb;
using DuckDB.NET.Data;

namespace DeltaZulu.Platform.Tests.Analytics;

[TestClass]
public sealed class DuckDbValueReaderTests
{
    [TestMethod]
    [DataRow("0.000000000000000000000000000001", 30)]
    [DataRow("79228162514264337593543950336", 0)]
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
                {'a': 1, 'b': 'x'} AS StructValue,
                [1, 2, 3] AS ListValue;
            """;

        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());

        Assert.IsInstanceOfType<string>(DuckDbValueReader.ReadValue(reader, 0));
        Assert.IsInstanceOfType<string>(DuckDbValueReader.ReadValue(reader, 1));
        Assert.IsInstanceOfType<string>(DuckDbValueReader.ReadValue(reader, 2));
    }

    [TestMethod]
    public void ReadValue_NormalizesInetExtensionValuesToStrings()
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();

        using var install = connection.CreateCommand();
        install.CommandText = "INSTALL inet; LOAD inet;";
        install.ExecuteNonQuery();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT '127.0.0.1'::INET AS IpAddress";

        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());

        var value = DuckDbValueReader.ReadValue(reader, 0);

        Assert.IsInstanceOfType<string>(value);
        Assert.Contains("127.0.0.1", (string)value!);
    }
}