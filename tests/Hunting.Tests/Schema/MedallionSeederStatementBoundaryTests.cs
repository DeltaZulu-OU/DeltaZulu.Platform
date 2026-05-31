namespace Hunting.Tests.Schema;

using Hunting.Core.DuckDbSql;
using Hunting.Data;
using Hunting.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class MedallionSeederStatementBoundaryTests
{
    [TestMethod]
    public void MockDataSeeder_CombinedSeedSql_SeparatesInsertStatements()
    {
        var sql = MockDataSeeder.GetMedallionSeedSql();

        Assert.Contains(
            ";\n\nINSERT INTO bronze.windows_security_event",
            sql,
            "Combined seed SQL must terminate the Sysmon INSERT before the Windows Security INSERT.");

        Assert.Contains(
            ";\n\nINSERT INTO bronze.dns_server_event",
            sql,
            "Combined seed SQL must terminate the Windows Security INSERT before the DNS server INSERT.");

        Assert.IsTrue(sql.TrimEnd().EndsWith(';'), "Combined seed SQL should terminate the final statement.");
    }

    [TestMethod]
    public void MockDataSeeder_CombinedSeedSql_ExecutesAsMultipleStatements()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = new SchemaApplier(factory);
        var ddl = new SchemaEmitter().EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: [],
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        applier.ApplyStatements(ddl);
        applier.ExecuteRaw(MockDataSeeder.GetMedallionSeedSql());

        var expected = MockDataSeeder.GetExpectedMedallionRowCountsByTable();

        Assert.AreEqual(expected["bronze.windows_sysmon_event"], applier.QueryScalar("SELECT count(*) FROM bronze.windows_sysmon_event"));
        Assert.AreEqual(expected["bronze.windows_security_event"], applier.QueryScalar("SELECT count(*) FROM bronze.windows_security_event"));
        Assert.AreEqual(expected["bronze.dns_server_event"], applier.QueryScalar("SELECT count(*) FROM bronze.dns_server_event"));

        Assert.IsGreaterThan(0, applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent"));
        Assert.IsGreaterThan(0, applier.QueryScalar("SELECT count(*) FROM golden.NetworkSession"));
        Assert.IsGreaterThan(0, applier.QueryScalar("SELECT count(*) FROM golden.Dns"));
    }
}