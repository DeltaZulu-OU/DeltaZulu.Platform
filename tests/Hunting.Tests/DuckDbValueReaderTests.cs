namespace Hunting.Tests;

using DuckDB.NET.Data;
using Hunting.Data;

[TestClass]
public sealed class DuckDbValueReaderTests
{
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