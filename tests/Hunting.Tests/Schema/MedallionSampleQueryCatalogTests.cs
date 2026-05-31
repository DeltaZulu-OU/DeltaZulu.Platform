namespace Hunting.Tests.Schema;

using Hunting.Core.Catalog;
using Hunting.Core.DuckDbSql;
using Hunting.Core.Samples;
using Hunting.Data;
using Hunting.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class MedallionSampleQueryCatalogTests
{
    private static readonly string[] ActiveGoldenTables =
    [
        "ProcessEvent",
        "NetworkSession",
        "Dns"
    ];

    private static readonly string[] LegacyNames =
    [
        "ProcessEvents",
        "NetworkSessions",
        "DeviceProcessEvents",
        "DeviceNetworkEvents",
        "windows_event_json"
    ];

    [TestMethod]
    public void SampleQueryCatalog_ContainsOnlyActiveGoldenTableNames()
    {
        Assert.IsTrue(SampleQueryCatalog.All.Count > 0);

        foreach (var sample in SampleQueryCatalog.All)
        {
            foreach (var legacyName in LegacyNames)
            {
                Assert.DoesNotContain(legacyName, sample.Kql);
            }

            Assert.IsTrue(
                ActiveGoldenTables.Any(table => sample.Kql.StartsWith(table, StringComparison.Ordinal)),
                $"{sample.Label} should start from one of the active Golden tables.");
        }
    }

    [TestMethod]
    public void SampleQueryCatalog_DoesNotUseStringEmptyCheckOnNumericFields()
    {
        foreach (var sample in SampleQueryCatalog.All)
        {
            Assert.DoesNotContain("isnotempty(ProcessId)", sample.Kql);
            Assert.DoesNotContain("isnotempty(RemotePort)", sample.Kql);
            Assert.DoesNotContain("isnotempty(LocalPort)", sample.Kql);
        }
    }

    [TestMethod]
    public void SampleQueryCatalog_DoesNotUseRelativeTimeFiltersAgainstFixedSeedData()
    {
        foreach (var sample in SampleQueryCatalog.All)
        {
            Assert.DoesNotContain("ago(", sample.Kql);
            Assert.DoesNotContain("now()", sample.Kql);
        }
    }

    [TestMethod]
    public void SampleQueryCatalog_HasSamplesForEveryActiveGoldenContract()
    {
        foreach (var table in ActiveGoldenTables)
        {
            Assert.IsTrue(
                SampleQueryCatalog.All.Any(sample => sample.Kql.StartsWith(table, StringComparison.Ordinal)),
                $"Expected at least one sample query for {table}.");
        }
    }

    [TestMethod]
    public void SampleQueryCatalog_AllQueriesExecuteAgainstSeededMedallionSchema()
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

        var catalog = new ApprovedViewCatalog();
        SchemaConventions.RegisterCanonicalViews(catalog);

        var runtime = new QueryRuntime(catalog, factory, defaultLimit: 10_000, developerMode: true);

        foreach (var sample in SampleQueryCatalog.All)
        {
            var result = runtime.Execute(sample.Kql);

            if (!result.Success)
            {
                var errors = string.Join(Environment.NewLine, result.Diagnostics.Errors.Select(static error => error.Message));
                Assert.Fail($"Sample query failed: {sample.Label}{Environment.NewLine}{sample.Kql}{Environment.NewLine}{errors}");
            }
        }
    }
}
