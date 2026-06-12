using System.Globalization;
using Dapper;
using DeltaZulu.Platform.Data.DuckDb;
using Microsoft.Data.Sqlite;

namespace DeltaZulu.Platform.Tests.Analytics.Spike;
[TestClass]
public sealed class DuckDbConnectionFactoryTests
{
    [TestMethod]
    [Description("Default startup SQL loads packaged inet extension and enables INET casts")]
    public void DefaultStartupSql_LoadsInetExtension()
    {
        using var factory = new DuckDbConnectionFactory("DataSource=:memory:");
        using var cmd = factory.GetConnection().CreateCommand();
        cmd.CommandText = "INSTALL inet;LOAD inet;";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "SELECT host('10.1.2.3/24'::INET)";
        var host = cmd.ExecuteScalar()?.ToString();

        Assert.AreEqual("10.1.2.3", host);
    }

    [TestMethod]
    [Description("Custom startup SQL can be empty")]
    public void CustomStartupSql_CanBeEmpty()
    {
        using var factory = new DuckDbConnectionFactory("DataSource=:memory:", []);
        using var cmd = factory.GetConnection().CreateCommand();
        cmd.CommandText = "SELECT 1";

        var value = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        Assert.AreEqual(1, value);
    }

    [TestMethod]
    [Description("Startup SQL executes during first connection initialization")]
    public void CustomStartupSql_RunsOnInitialize()
    {
        using var factory = new DuckDbConnectionFactory("DataSource=:memory:", [
            "CREATE TEMP TABLE startup_probe(id INTEGER)",
            "INSERT INTO startup_probe VALUES (7)"]);

        using var cmd = factory.GetConnection().CreateCommand();
        cmd.CommandText = "SELECT id FROM startup_probe";

        var value = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        Assert.AreEqual(7, value);
    }

    [TestMethod]
    public void GetConnection_AttachesSqliteDatabaseAndCreatesDuckDbViewsForCrossDatabaseSelects()
    {
        var sqlitePath = Path.Combine(Path.GetTempPath(), $"duckdb-attached-sqlite-{Guid.NewGuid():N}.db");
        try
        {
            using (var sqlite = new SqliteConnection($"Data Source={sqlitePath}"))
            {
                sqlite.Open();
                sqlite.Execute("CREATE TABLE lookup_values (id TEXT PRIMARY KEY, display_name TEXT NOT NULL);");
                sqlite.Execute("INSERT INTO lookup_values (id, display_name) VALUES ('severity-high', 'High');");
            }

            using var factory = new DuckDbConnectionFactory(
                "DataSource=:memory:",
                [],
                [new DuckDbAttachedDatabase(
                    "lookup",
                    sqlitePath,
                    readOnly: true,
                    views: [new DuckDbAttachedView("lookup_values", "lookup_values", "lookups")])]);

            var connection = factory.GetConnection();
            var displayName = connection.ExecuteScalar<string>(
                "SELECT display_name FROM lookups.lookup_values WHERE id = 'severity-high';");

            Assert.AreEqual("High", displayName);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(sqlitePath))
            {
                File.Delete(sqlitePath);
            }
        }
    }

    [TestMethod]
    public void Constructor_RejectsDuplicateAttachedDatabaseAliases()
    {
        var ex = Assert.ThrowsExactly<ArgumentException>(() => new DuckDbConnectionFactory(
            "DataSource=:memory:",
            [],
            [
                new DuckDbAttachedDatabase("app", "/tmp/app-a.db"),
                new DuckDbAttachedDatabase("APP", "/tmp/app-b.db")
            ]));

        Assert.Contains("Duplicate attached DuckDB database alias 'APP'.", ex.Message);
    }

    [TestMethod]
    public void Constructor_RejectsDuplicateAttachedViewTargets()
    {
        var ex = Assert.ThrowsExactly<ArgumentException>(() => new DuckDbConnectionFactory(
            "DataSource=:memory:",
            [],
            [
                new DuckDbAttachedDatabase(
                    "app",
                    "/tmp/app.db",
                    views: [
                        new DuckDbAttachedView("saved_queries", "saved_queries", "app_state"),
                        new DuckDbAttachedView("SAVED_QUERIES", "saved_queries_archive", "APP_STATE")])
            ]));

        Assert.Contains("Duplicate attached DuckDB view target 'APP_STATE.SAVED_QUERIES'.", ex.Message);
    }
}
