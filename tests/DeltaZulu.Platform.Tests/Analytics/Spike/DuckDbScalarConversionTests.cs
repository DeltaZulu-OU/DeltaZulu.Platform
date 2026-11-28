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

    private static DuckDBConnection Open()
    {
        var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        return connection;
    }
}
