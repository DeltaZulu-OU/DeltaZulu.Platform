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

    [TestMethod]
    public void NativeJsonPreservesWideDecimalTokensAndNulls()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT to_json({
                'tiny': CAST('0.000000000000000000000000000001' AS DECIMAL(38,30)),
                'wide': CAST('99999999999999999999999999999999999999' AS DECIMAL(38,0)),
                'items': [CAST('-0.000000000000000000000000000001' AS DECIMAL(38,30)), NULL],
                'missing': NULL::INTEGER
            }), to_json(NULL::STRUCT(x INTEGER))
            """;
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        using var json = System.Text.Json.JsonDocument.Parse(reader.GetString(0));
        var root = json.RootElement;
        Assert.AreEqual("0.000000000000000000000000000001", root.GetProperty("tiny").GetRawText());
        Assert.AreEqual("99999999999999999999999999999999999999", root.GetProperty("wide").GetRawText());
        Assert.AreEqual("-0.000000000000000000000000000001", root.GetProperty("items")[0].GetRawText());
        Assert.AreEqual(System.Text.Json.JsonValueKind.Null, root.GetProperty("items")[1].ValueKind);
        Assert.AreEqual(System.Text.Json.JsonValueKind.Null, root.GetProperty("missing").ValueKind);
        Assert.IsTrue(reader.IsDBNull(1));
    }

    [TestMethod]
    public void NativeJsonPreservesArrayAndStringKeyedMap()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT to_json([1, NULL, 3]::INTEGER[3]),
                   to_json(map(['a', 'b'], [CAST('0.000000000000000000000000000001' AS DECIMAL(38,30)), NULL]))
            """;
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        using var array = System.Text.Json.JsonDocument.Parse(reader.GetString(0));
        Assert.AreEqual(1, array.RootElement[0].GetInt32());
        Assert.AreEqual(System.Text.Json.JsonValueKind.Null, array.RootElement[1].ValueKind);
        Assert.AreEqual(3, array.RootElement[2].GetInt32());
        using var map = System.Text.Json.JsonDocument.Parse(reader.GetString(1));
        Assert.AreEqual("0.000000000000000000000000000001", map.RootElement.GetProperty("a").GetRawText());
        Assert.AreEqual(System.Text.Json.JsonValueKind.Null, map.RootElement.GetProperty("b").ValueKind);
    }

    [TestMethod]
    public void NativeJsonCharacterizesNonJsonScalarTypes()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSTALL inet; LOAD inet;";
        command.ExecuteNonQuery();
        command.CommandText = """
            SELECT to_json({'x': 'NaN'::DOUBLE, 'y': 'Infinity'::DOUBLE, 'z': '-Infinity'::DOUBLE}),
                   to_json({'ip': '2001:db8::1/64'::INET, 'id': '01234567-89ab-cdef-0123-456789abcdef'::UUID}),
                   to_json({'t': TIMESTAMP_NS '2026-01-01 00:00:00.1234567', 'd': INTERVAL '-1 microsecond'}),
                   to_json(map([1, 2], ['a', 'b']))
            """;
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var text = reader.GetString(i);
            Console.WriteLine($"Native JSON {i}: {text}");
            try
            {
                using var json = System.Text.Json.JsonDocument.Parse(text);
                Console.WriteLine($"Native JSON {i}: valid strict JSON");
            }
            catch (System.Text.Json.JsonException)
            {
                Console.WriteLine($"Native JSON {i}: not valid strict JSON");
            }
        }
    }

    [TestMethod]
    [DataRow("127.0.0.1")]
    [DataRow("2001:db8::1/64")]
    [DataRow("::")]
    [DataRow("ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff/127")]
    public void NativeInetCastPreservesAddress(string address)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSTALL inet; LOAD inet;";
        command.ExecuteNonQuery();
        command.CommandText = $"SELECT CAST('{address}'::INET AS VARCHAR), CAST(NULL::INET AS VARCHAR)";
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(address, reader.GetString(0));
        Assert.IsTrue(reader.IsDBNull(1));
    }

    [TestMethod]
    [DataRow(1234560L)]
    [DataRow(-1234560L)]
    [DataRow(863999999990L)]
    [DataRow(-863999999990L)]
    public void TemporalParametersRoundTripAfterReopen(long ticks)
    {
        var path = Path.Combine(Path.GetTempPath(), $"dz-conversion-{Guid.NewGuid():N}.duckdb");
        var instant = new DateTimeOffset(2026, 1, 1, 2, 0, 0, TimeSpan.FromHours(2)).AddTicks(ticks);
        var duration = TimeSpan.FromTicks(ticks);
        try
        {
            using (var connection = new DuckDBConnection($"DataSource={path}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE values_probe (instant TIMESTAMPTZ, duration INTERVAL)";
                command.ExecuteNonQuery();
                command.CommandText = "INSERT INTO values_probe VALUES (?, ?)";
                command.Parameters.Add(new DuckDBParameter(instant));
                command.Parameters.Add(new DuckDBParameter(duration));
                command.ExecuteNonQuery();
            }

            using var reopened = new DuckDBConnection($"DataSource={path}");
            reopened.Open();
            using var read = reopened.CreateCommand();
            read.CommandText = "SELECT instant, duration FROM values_probe";
            using var reader = read.ExecuteReader();
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(instant.UtcDateTime, DeltaZulu.Platform.Data.DuckDb.DuckDbValueReader.ReadValue(reader, 0));
            Assert.AreEqual(duration, DeltaZulu.Platform.Data.DuckDb.DuckDbValueReader.ReadValue(reader, 1));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".wal");
        }
    }

    [TestMethod]
    [DataRow("0.123456789012345", 15)]
    [DataRow("-0.123456789012345", 15)]
    [DataRow("999999999999999", 0)]
    [DataRow("9999999999.99999", 5)]
    [DataRow("0.000000000000001", 15)]
    [DataRow("0.29", 2)]
    public void NativeJsonPreservesNarrowDecimalTokens(string value, int scale)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT to_json({{'value': CAST('{value}' AS DECIMAL(15,{scale}))}})";
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        using var json = System.Text.Json.JsonDocument.Parse(reader.GetString(0));
        Assert.AreEqual(decimal.Parse(value, CultureInfo.InvariantCulture), json.RootElement.GetProperty("value").GetDecimal());
    }

    [TestMethod]
    [DataRow(1L)]
    [DataRow(-1L)]
    [DataRow(9L)]
    [DataRow(-9L)]
    public void TemporalParameterCharacterizesSubmicrosecondPrecision(long ticks)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        var instant = DateTimeOffset.UnixEpoch.AddTicks(ticks);
        var duration = TimeSpan.FromTicks(ticks);
        command.CommandText = "SELECT CAST(? AS TIMESTAMPTZ), CAST(? AS INTERVAL)";
        command.Parameters.Add(new DuckDBParameter(instant));
        command.Parameters.Add(new DuckDBParameter(duration));
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        var observedInstant = reader.GetFieldValue<DateTimeOffset>(0);
        var observedDuration = reader.GetFieldValue<TimeSpan>(1);
        Console.WriteLine($"Temporal parameter: input ticks={ticks}; instant delta ticks={(observedInstant - DateTimeOffset.UnixEpoch).Ticks}; duration ticks={observedDuration.Ticks}");
    }

    [TestMethod]
    public void TimestampNsCharacterizesSubtickPrecision()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TIMESTAMP_NS '2026-01-01 00:00:00.123456789',
                   CAST(TIMESTAMP_NS '2026-01-01 00:00:00.123456789' AS VARCHAR),
                   TIMESTAMP_NS '1969-12-31 23:59:59.999999999',
                   CAST(TIMESTAMP_NS '1969-12-31 23:59:59.999999999' AS VARCHAR)
            """;
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        Console.WriteLine($"TimestampNs: native={reader.GetString(1)}; managed={reader.GetDateTime(0):O}");
        Console.WriteLine($"TimestampNs: native={reader.GetString(3)}; managed={reader.GetDateTime(2):O}");
    }

    private static DuckDBConnection Open()
    {
        var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        return connection;
    }
}
