using DuckDB.NET.Data;
using System.Globalization;

namespace DeltaZulu.Platform.Tests.Analytics.Spike;

[TestClass]
public sealed class DuckDbScalarConversionTests
{
    [TestMethod]
    public void RecordProviderAndNativeVersions()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT version()";
        Console.WriteLine($"Native: {command.ExecuteScalar()}; Managed: {typeof(DuckDBConnection).Assembly.FullName}");
    }

    [TestMethod]
    public void TimestampReaderCharacterizesKindAndPrecision()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT TIMESTAMP '2026-01-01 00:00:00.1234567', TIMESTAMP_NS '2026-01-01 00:00:00.1234567'";
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        for (var i = 0; i < 2; i++)
        {
            var value = reader.GetDateTime(i);
            Console.WriteLine($"{reader.GetDataTypeName(i)}: {value:O}; kind={value.Kind}; ticks={value.Ticks}");
        }
    }

    [TestMethod]
    public void DecimalReaderCharacterizesHighScale()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT CAST('0.000000000000000000000000000001' AS DECIMAL(38,30))";
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        Console.WriteLine($"Decimal: {reader.GetDecimal(0).ToString(CultureInfo.InvariantCulture)}");
    }

    [TestMethod]
    public void IntervalParameterCharacterizesNegativeDuration()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(? AS INTERVAL)";
        command.Parameters.Add(new DuckDBParameter(TimeSpan.FromSeconds(-1)));
        try
        {
            using var reader = command.ExecuteReader();
            Assert.IsTrue(reader.Read());
            Console.WriteLine($"Interval: {reader.GetValue(0)}");
        }
        catch (Exception ex) when (ex is OverflowException or DuckDBException or NotSupportedException or InvalidCastException or ArgumentException)
        {
            Console.WriteLine($"Interval conversion: {ex.GetType().Name}: {ex.Message}");
        }
    }

    [TestMethod]
    public void LogicalMetadataDistinguishesInetFromOrdinaryStruct()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSTALL inet; LOAD inet;";
        command.ExecuteNonQuery();
        const string sql = "SELECT '2001:db8::1/64'::INET AS Address, {'ip_type': 2, 'address': 1::UHUGEINT, 'mask': 64} AS OrdinaryStruct";
        command.CommandText = "DESCRIBE " + sql;
        using (var metadata = command.ExecuteReader())
        {
            Assert.IsTrue(metadata.Read());
            Assert.AreEqual("INET", metadata.GetString(1));
            Assert.IsTrue(metadata.Read());
            Assert.IsTrue(metadata.GetString(1).StartsWith("STRUCT", StringComparison.Ordinal));
            Assert.IsFalse(metadata.Read());
        }
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        for (var i = 0; i < reader.FieldCount; i++)
        {
            Assert.AreEqual("Struct", reader.GetDataTypeName(i));
            var fields = reader.GetFieldValue<Dictionary<string, object>>(i);
            Console.WriteLine($"{reader.GetName(i)}: {string.Join(", ", fields.Select(p => $"{p.Key}={p.Value} ({p.Value?.GetType().Name})"))}");
        }
    }

    private static DuckDBConnection Open()
    {
        var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        return connection;
    }
}
