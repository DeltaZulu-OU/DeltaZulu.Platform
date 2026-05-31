namespace Hunting.Tests.Schema;

using Hunting.Core.DuckDbSql;
using Hunting.Data;
using Hunting.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Text.RegularExpressions;

[TestClass]
public sealed class MedallionPhase1AValidationGuardTests
{
    private static readonly string[] ExpectedBronzeTables =
    [
        "bronze.windows_sysmon_event",
        "bronze.windows_security_event",
        "bronze.dns_server_event"
    ];

    private static readonly string[] ExpectedSilverViews =
    [
        "silver.v_processevent_windows_sysmon_eid1",
        "silver.v_processevent_windows_security_eid4688",
        "silver.v_networksession_windows_sysmon_eid3",
        "silver.v_networksession_windows_security_eid5156",
        "silver.v_dns_windows_sysmon_eid22",
        "silver.v_dns_server_query_event"
    ];

    private static readonly string[] ExpectedGoldenViews =
    [
        "golden.ProcessEvent",
        "golden.NetworkSession",
        "golden.Dns"
    ];

    private static readonly string[] LegacyObjectFragments =
    [
        "windows_event_json",
        "v_process_sysmon_create",
        "ProcessEvents",
        "NetworkSessions",
        "DeviceProcessEvents",
        "DeviceNetworkEvents"
    ];

    [TestMethod]
    public void Phase1A_ActiveSurface_ContainsExactlyExpectedBronzeSilverGoldenObjects()
    {
        CollectionAssert.AreEquivalent(
            ExpectedBronzeTables,
            SchemaConventions.RawTables.Select(static table => table.QualifiedName).ToArray());

        CollectionAssert.AreEquivalent(
            ExpectedSilverViews,
            SchemaConventions.ParserViews.Select(static view => view.QualifiedName).ToArray());

        CollectionAssert.AreEquivalent(
            ExpectedGoldenViews,
            SchemaConventions.CanonicalViews.Select(static view => view.QualifiedName).ToArray());
    }

    [TestMethod]
    public void Phase1A_ActiveSurface_DoesNotExposeLegacyVerticalSliceNames()
    {
        var activeObjectNames = SchemaConventions.RawTables.Select(static table => table.QualifiedName)
            .Concat(SchemaConventions.ParserViews.Select(static view => view.QualifiedName))
            .Concat(SchemaConventions.CanonicalViews.Select(static view => view.QualifiedName))
            .ToArray();

        foreach (var objectName in activeObjectNames)
        {
            foreach (var legacyFragment in LegacyObjectFragments)
            {
                Assert.IsFalse(
                    objectName.Contains(legacyFragment, StringComparison.OrdinalIgnoreCase),
                    $"{objectName} should not contain legacy fragment {legacyFragment}.");
            }
        }
    }

    [TestMethod]
    public void Phase1A_SchemaEmitter_DoesNotEmitLegacyVerticalSliceNames()
    {
        var ddl = new SchemaEmitter().EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: [],
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        var combined = string.Join("\n", ddl);

        foreach (var legacyFragment in LegacyObjectFragments)
        {
            Assert.DoesNotContain(legacyFragment, combined);
        }

        foreach (var activeObject in ExpectedBronzeTables.Concat(ExpectedSilverViews).Concat(ExpectedGoldenViews))
        {
            Assert.Contains(activeObject, combined);
        }
    }

    [TestMethod]
    public void Phase1A_MockDataSeeder_DoesNotExposeLegacySeedEntryPoints()
    {
        var publicStaticMethodNames = typeof(MockDataSeeder)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(static method => method.Name)
            .ToArray();

        Assert.IsFalse(publicStaticMethodNames.Contains("GetProcessSeedSql"));
        Assert.IsFalse(publicStaticMethodNames.Contains("GetLegacyProcessSeedSql"));
        Assert.IsTrue(publicStaticMethodNames.Contains("GetMedallionSeedSql"));
        Assert.IsTrue(publicStaticMethodNames.Contains("GetMedallionSeedSqlByTable"));
    }

    [TestMethod]
    public void Phase1A_MockDataSeeder_CombinedSeedSqlTargetsExactlyThreeActiveBronzeTables()
    {
        var sql = MockDataSeeder.GetMedallionSeedSql();
        var insertTargets = Regex.Matches(sql, @"INSERT\s+INTO\s+([A-Za-z0-9_]+\.[A-Za-z0-9_]+)", RegexOptions.IgnoreCase)
            .Select(static match => match.Groups[1].Value)
            .ToArray();

        CollectionAssert.AreEquivalent(ExpectedBronzeTables, insertTargets);

        foreach (var legacyFragment in LegacyObjectFragments)
        {
            Assert.DoesNotContain(legacyFragment, sql);
        }

        Assert.IsTrue(sql.TrimEnd().EndsWith(";", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Phase1A_MockDataSeeder_PerTableSeedSqlAndExpectedCountsStayAligned()
    {
        var seedSqlByTable = MockDataSeeder.GetMedallionSeedSqlByTable();
        var expectedCountsByTable = MockDataSeeder.GetExpectedMedallionRowCountsByTable();

        CollectionAssert.AreEquivalent(ExpectedBronzeTables, seedSqlByTable.Keys.ToArray());
        CollectionAssert.AreEquivalent(ExpectedBronzeTables, expectedCountsByTable.Keys.ToArray());

        foreach (var tableName in ExpectedBronzeTables)
        {
            Assert.IsTrue(expectedCountsByTable[tableName] > 0, $"{tableName} should have development seed rows.");
            Assert.Contains($"INSERT INTO {tableName}", seedSqlByTable[tableName]);
            Assert.IsTrue(seedSqlByTable[tableName].TrimEnd().EndsWith(";", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void Phase1A_SeededMedallionSchema_HasRowsInEveryActiveSourceAndGoldenView()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateSchema(factory);

        applier.ExecuteRaw(MockDataSeeder.GetMedallionSeedSql());

        foreach (var tableName in ExpectedBronzeTables)
        {
            var expectedCount = MockDataSeeder.GetExpectedMedallionRowCountsByTable()[tableName];
            var actualCount = applier.QueryScalar($"SELECT count(*) FROM {tableName}");

            Assert.AreEqual(expectedCount, actualCount, $"{tableName} should match its declared seed count.");
        }

        foreach (var viewName in ExpectedGoldenViews)
        {
            var rowCount = applier.QueryScalar($"SELECT count(*) FROM {viewName}");
            Assert.IsTrue(rowCount > 0, $"{viewName} should have at least one seeded row.");
        }
    }

    [TestMethod]
    public void Phase1A_SeededMedallionSchema_CoversRepresentativeSampleScenarios()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = CreateSchema(factory);

        applier.ExecuteRaw(MockDataSeeder.GetMedallionSeedSql());

        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent WHERE FileName = 'powershell.exe' AND lower(ProcessCommandLine) LIKE '%enc%'") >= 2);
        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent WHERE FileName IN ('mimikatz.exe', 'rundll32.exe', 'procdump.exe')") >= 3);
        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent WHERE FileName IN ('schtasks.exe', 'reg.exe', 'sc.exe', 'at.exe')") >= 4);
        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.NetworkSession WHERE RemoteIP = '203.0.113.60' AND RemoteUrl IS NULL") >= 4);
        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.NetworkSession WHERE RemotePort IN (4444, 445, 53)") >= 3);
        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.Dns WHERE QueryName LIKE '%example.test%'") >= 3);
        Assert.IsTrue(applier.QueryScalar("SELECT count(*) FROM golden.Dns WHERE ResponseCode = 'NXDOMAIN'") >= 1);
    }

    private static SchemaApplier CreateSchema(DuckDbConnectionFactory factory)
    {
        var applier = new SchemaApplier(factory);
        var ddl = new SchemaEmitter().EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: [],
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        applier.ApplyStatements(ddl);
        return applier;
    }
}
