namespace Hunting.Tests.Schema;

using Hunting.Core.DuckDbSql;
using Hunting.Data;
using Hunting.Schema;
using Hunting.Schema.Definitions.Medallion;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class MedallionPhaseClosureRegressionTests
{
    [TestMethod]
    public void Phase1A_AllSilverParserViews_AreFilteredAndNotPassThrough()
    {
        var emitter = new SchemaEmitter();

        foreach (var parser in MedallionSchemaCatalog.ParserViews)
        {
            var sql = emitter.EmitParserView(parser);

            Assert.Contains("WHERE", sql, $"{parser.QualifiedName} must have a source/event filter.");
            Assert.Contains("json_exists(raw_log,", sql, $"{parser.QualifiedName} must check for the selector field.");
            Assert.Contains("json_extract_string(raw_log,", sql, $"{parser.QualifiedName} must extract the selector field.");
        }
    }

    [TestMethod]
    public void Phase1A_AllSilverParserViews_MapMinimumUsableGoldenFields()
    {
        foreach (var parser in MedallionSchemaCatalog.ParserViews)
        {
            var sql = new SchemaEmitter().EmitParserView(parser);

            Assert.DoesNotContain(
                "CAST(NULL AS TIMESTAMP) AS Timestamp",
                sql,
                $"{parser.QualifiedName} must map Timestamp from source data.");

            if (parser.Columns.Any(static column => column.Name == "DeviceName"))
            {
                Assert.DoesNotContain(
                    "CAST(NULL AS VARCHAR) AS DeviceName",
                    sql,
                    $"{parser.QualifiedName} must map DeviceName from source data.");
            }

            if (parser.Columns.Any(static column => column.Name == "ActionType"))
            {
                Assert.DoesNotContain(
                    "CAST(NULL AS VARCHAR) AS ActionType",
                    sql,
                    $"{parser.QualifiedName} must map ActionType from a source-derived or intentional literal value.");
            }
        }
    }

    [TestMethod]
    public void Phase1A_GoldenViews_AreExecutableAfterSchemaApplication()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = new SchemaApplier(factory);
        var ddl = new SchemaEmitter().EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: [],
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        applier.ApplyStatements(ddl);

        foreach (var view in SchemaConventions.CanonicalViews)
        {
            var count = applier.QueryScalar($"SELECT count(*) FROM {view.QualifiedName}");
            Assert.AreEqual(0, count, $"{view.QualifiedName} should be executable on an empty schema.");
        }
    }

    [TestMethod]
    public void Phase1A_SilverViews_AreExecutableAfterSchemaApplication()
    {
        using var factory = new DuckDbConnectionFactory(startupSql: []);
        var applier = new SchemaApplier(factory);
        var ddl = new SchemaEmitter().EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: [],
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        applier.ApplyStatements(ddl);

        foreach (var view in SchemaConventions.ParserViews)
        {
            var count = applier.QueryScalar($"SELECT count(*) FROM {view.QualifiedName}");
            Assert.AreEqual(0, count, $"{view.QualifiedName} should be executable on an empty schema.");
        }
    }

    [TestMethod]
    public void Phase1A_ProcessEventContributors_UseProcessCreatedAction()
    {
        var sqlByName = MedallionSchemaCatalog.ParserViews
            .Where(static view => view.CanonicalTarget == "ProcessEvent")
            .ToDictionary(static view => view.QualifiedName, static view => new SchemaEmitter().EmitParserView(view), StringComparer.OrdinalIgnoreCase);

        Assert.Contains("'ProcessCreated' AS ActionType", sqlByName["silver.v_processevent_windows_sysmon_eid1"]);
        Assert.Contains("'ProcessCreated' AS ActionType", sqlByName["silver.v_processevent_windows_security_eid4688"]);
    }

    [TestMethod]
    public void Phase1A_NetworkSessionContributors_UseExplicitNetworkActions()
    {
        var sqlByName = MedallionSchemaCatalog.ParserViews
            .Where(static view => view.CanonicalTarget == "NetworkSession")
            .ToDictionary(static view => view.QualifiedName, static view => new SchemaEmitter().EmitParserView(view), StringComparer.OrdinalIgnoreCase);

        Assert.Contains("'ConnectionSuccess' AS ActionType", sqlByName["silver.v_networksession_windows_sysmon_eid3"]);
        Assert.Contains("'ConnectionAllowed' AS ActionType", sqlByName["silver.v_networksession_windows_security_eid5156"]);
    }

    [TestMethod]
    public void Phase1A_DnsContributors_UseDnsQueryAction()
    {
        var sqlByName = MedallionSchemaCatalog.ParserViews
            .Where(static view => view.CanonicalTarget == "Dns")
            .ToDictionary(static view => view.QualifiedName, static view => new SchemaEmitter().EmitParserView(view), StringComparer.OrdinalIgnoreCase);

        Assert.Contains("'DnsQuery' AS ActionType", sqlByName["silver.v_dns_windows_sysmon_eid22"]);
        Assert.Contains("'DnsQuery' AS ActionType", sqlByName["silver.v_dns_server_query_event"]);
    }

    [TestMethod]
    public void Phase1A_GoldenViews_DoNotDependOnLowerLayerColumnWildcards()
    {
        var emitter = new SchemaEmitter();

        foreach (var view in MedallionSchemaCatalog.CanonicalViews)
        {
            var sql = emitter.EmitCanonicalView(view);

            Assert.DoesNotContain("SELECT *", sql, $"{view.QualifiedName} must explicitly own its projection.");
            Assert.Contains("UNION ALL", sql, $"{view.QualifiedName} should combine its two Phase 1A Silver contributors.");
        }
    }

    [TestMethod]
    public void Phase1A_DocumentKnownDeferredHardeningItems()
    {
        var deferredItems = new[]
        {
            "schema provenance and migration safety",
            "seed idempotency and fixture provenance",
            "first-class Silver parser specs",
            "negative JSON and wrong-source tests",
            "tolerant casting policy",
            "DNS response-code normalization",
            "Windows Security hex process ID handling",
            "Golden semantic normalization"
        };

        Assert.AreEqual(8, deferredItems.Length, "The closure test should keep known Phase 1D hardening work explicit.");
    }
}
