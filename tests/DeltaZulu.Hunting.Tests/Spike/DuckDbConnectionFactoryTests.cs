namespace DeltaZulu.Hunting.Tests.Spike;

using System.Globalization;
using DeltaZulu.Hunting.Data;

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
}