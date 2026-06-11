
using System.Globalization;
using DuckDB.NET.Data;

namespace DeltaZulu.Platform.Tests.Hunting.Spike;
/// <summary>
/// Phase 0 smoke test: verify DuckDB.NET opens an in-memory database
/// and executes a trivial query. If this fails, the runtime binding
/// or native library is broken and nothing downstream will work.
/// </summary>
[TestClass]
public sealed class DuckDbSmokeTests
{
    [TestMethod]
    [Description("In-memory DuckDB opens and executes SELECT 42")]
    public void InMemory_TrivialQuery()
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 42 AS answer";
        var result = cmd.ExecuteScalar();

        Assert.AreEqual(42, Convert.ToInt32(result, CultureInfo.InvariantCulture));
    }

    [TestMethod]
    [Description("Schema creation and view work as expected")]
    public void InMemory_SchemaAndView()
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();

        cmd.CommandText = "CREATE SCHEMA IF NOT EXISTS bronze";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "CREATE SCHEMA IF NOT EXISTS silver";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "CREATE SCHEMA IF NOT EXISTS golden";
        cmd.ExecuteNonQuery();

        cmd.CommandText =
            """
            CREATE TABLE bronze.test_events (
                ingest_time TIMESTAMP,
                source_type VARCHAR,
                payload JSON
            )
            """;
        cmd.ExecuteNonQuery();

        cmd.CommandText =
            """
            INSERT INTO bronze.test_events VALUES
                (TIMESTAMP '2025-01-01 00:00:00', 'sysmon', '{"Image":"cmd.exe"}')
            """;
        cmd.ExecuteNonQuery();

        cmd.CommandText =
            """
            CREATE VIEW silver.v_test AS
                SELECT ingest_time AS Timestamp,
                       source_type AS Source,
                       json_extract_string(payload, '$.Image') AS FileName
                FROM bronze.test_events
            """;
        cmd.ExecuteNonQuery();

        // golden schema is operator-facing in POC — create unifying view
        cmd.CommandText =
            """
            CREATE VIEW golden.TestEvents AS
                SELECT * FROM silver.v_test
            """;
        cmd.ExecuteNonQuery();

        // Query the public view
        cmd.CommandText = "SELECT FileName FROM golden.TestEvents LIMIT 1";
        var fileName = cmd.ExecuteScalar()?.ToString();

        Assert.AreEqual("cmd.exe", fileName);
    }

    [TestMethod]
    [Description("DESCRIBE returns column metadata for validation")]
    public void InMemory_DescribeView()
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();

        cmd.CommandText = "CREATE TABLE test_tbl (id BIGINT, name VARCHAR, ts TIMESTAMP)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "CREATE VIEW test_view AS SELECT * FROM test_tbl";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "DESCRIBE test_view";
        using var reader = cmd.ExecuteReader();

        var columns = new List<(string Name, string Type)>();
        while (reader.Read())
        {
            columns.Add((reader.GetString(0), reader.GetString(1)));
        }

        Assert.HasCount(3, columns);
        Assert.AreEqual("id", columns[0].Name);
        Assert.AreEqual("name", columns[1].Name);
        Assert.AreEqual("ts", columns[2].Name);
    }
}