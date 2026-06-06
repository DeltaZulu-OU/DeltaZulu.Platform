namespace Hunting.Tests.Fixtures;

using DuckDB.NET.Data;

/// <summary>
/// <para>Test-owned DuckDB setup helpers for medallion execution tests.</para>
/// <para>
/// Use this helper when the behavior under test needs data but does not need the
/// large development/demo corpus from MockDataSeeder. Tests that validate the
/// production schema can still apply the real schema first and then call
/// SeedMedallionTestData(...).
/// </para>
/// </summary>
internal static class MedallionTestDatabase
{
    public static long CountRows(DuckDBConnection connection, string tableName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT count(*) FROM {tableName};";
        return Convert.ToInt64(command.ExecuteScalar());
    }

    public static void CreateBronzeTables(DuckDBConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ExecuteNonQuery(connection, MedallionTestData.CreateBronzeSchemaAndTablesSql());
    }

    public static DuckDBConnection CreateOpenConnectionWithBronzeTestData()
    {
        var connection = new DuckDBConnection("Data Source=:memory:");
        connection.Open();
        CreateBronzeTables(connection);
        SeedMedallionTestData(connection);
        return connection;
    }

    public static void SeedMedallionTestData(DuckDBConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        foreach (var sql in MedallionTestData.GetSeedSqlByTable().Values)
        {
            ExecuteNonQuery(connection, sql);
        }
    }

    private static void ExecuteNonQuery(DuckDBConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
