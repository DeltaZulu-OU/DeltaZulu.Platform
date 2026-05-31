namespace Hunting.Tests.Schema;

using Hunting.Core.DuckDbSql;
using Hunting.Data;
using Hunting.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class MedallionSeederTests
{
    [TestMethod]
    public void MockDataSeeder_MedallionSeedSql_TargetsOnlyActiveBronzeTables()
    {
        var sqlByTable = MockDataSeeder.GetMedallionSeedSqlByTable();

        CollectionAssert.AreEquivalent(
            new[]
            {
                "bronze.windows_sysmon_event",
                "bronze.windows_security_event",
                "bronze.dns_server_event"
            },
            sqlByTable.Keys.ToArray());

        var combinedSql = MockDataSeeder.GetMedallionSeedSql();

        Assert.DoesNotContain("bronze.windows_event_json", combinedSql);
        Assert.DoesNotContain("source_type", combinedSql);
        Assert.DoesNotContain("event_data", combinedSql);
        Assert.Contains("bronze.windows_sysmon_event", combinedSql);
        Assert.Contains("bronze.windows_security_event", combinedSql);
        Assert.Contains("bronze.dns_server_event", combinedSql);
    }

    [TestMethod]
    public void MockDataSeeder_ExpectedMedallionRowCounts_MatchCurrentSeedCoverage()
    {
        var expected = MockDataSeeder.GetExpectedMedallionRowCountsByTable();

        Assert.AreEqual(32, expected["bronze.windows_sysmon_event"]);
        Assert.AreEqual(4, expected["bronze.windows_security_event"]);
        Assert.AreEqual(3, expected["bronze.dns_server_event"]);
    }

    [TestMethod]
    public void MockDataSeeder_MedallionSeedSql_CoversAllActiveSilverSelectors()
    {
        var sql = MockDataSeeder.GetMedallionSeedSql();

        Assert.Contains("\"EventID\":\"1\"", sql);
        Assert.Contains("\"EventID\":\"4688\"", sql);
        Assert.Contains("\"EventID\":\"3\"", sql);
        Assert.Contains("\"EventID\":\"5156\"", sql);
        Assert.Contains("\"EventID\":\"22\"", sql);
        Assert.Contains("\"opcode\":\"QUERY\"", sql);
    }

    [TestMethod]
    public void MockDataSeeder_MedallionSeedSql_CanPopulateGoldenViews()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndSeed(factory);

        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent") >= 20);
        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.NetworkSession") >= 10);
        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.Dns") >= 5);
    }

    [TestMethod]
    public void MockDataSeeder_MedallionSeedSql_CoversRepresentativeProcessSamples()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndSeed(factory);

        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent WHERE lower(ProcessCommandLine) LIKE '%encodedcommand%' OR lower(ProcessCommandLine) LIKE '% -enc %'") >= 2);
        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent WHERE lower(FileName) LIKE '%mimikatz.exe%'") >= 1);
        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent WHERE FileName IN ('schtasks.exe', 'reg.exe', 'sc.exe', 'at.exe')") >= 4);
        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent WHERE FileName IN ('mimikatz.exe', 'rundll32.exe', 'procdump.exe')") >= 3);
    }

    [TestMethod]
    public void MockDataSeeder_MedallionSeedSql_CoversRepresentativeNetworkSamples()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndSeed(factory);

        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.NetworkSession WHERE RemotePort = 4444") >= 1);
        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.NetworkSession WHERE RemotePort = 445") >= 1);
        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.NetworkSession WHERE RemotePort = 53 AND lower(InitiatingProcessFileName) LIKE '%powershell%'") >= 1);
        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.NetworkSession WHERE RemoteIP = '203.0.113.60' AND RemoteUrl IS NULL") >= 4);
    }

    [TestMethod]
    public void MockDataSeeder_MedallionSeedSql_CoversRepresentativeDnsSamples()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateAndSeed(factory);

        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.Dns WHERE QueryName = 'c2.example.test'") >= 2);
        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.Dns WHERE ResponseCode = 'NXDOMAIN'") >= 1);
        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.Dns WHERE QueryName LIKE '%example.test%'") >= 3);
    }

    private static SchemaApplier CreateAndSeed(DuckDbConnectionFactory factory)
    {
        var applier = new SchemaApplier(factory);
        var ddl = new SchemaEmitter().EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: [],
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        applier.ApplyStatements(ddl);
        applier.ExecuteRaw(MockDataSeeder.GetMedallionSeedSql());
        return applier;
    }
}
