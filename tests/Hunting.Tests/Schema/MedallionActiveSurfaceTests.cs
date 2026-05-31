namespace Hunting.Tests.Schema;

using Hunting.Schema.Definitions.Medallion;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class MedallionActiveSurfaceTests
{
    [TestMethod]
    public void MedallionSchemaCatalog_ActiveSurface_HasExpectedObjectCounts()
    {
        Assert.AreEqual(3, MedallionSchemaCatalog.RawTables.Count, "Phase 1A should expose exactly three Bronze source-family tables.");
        Assert.AreEqual(6, MedallionSchemaCatalog.ParserViews.Count, "Phase 1A should expose exactly six Silver parser contributors.");
        Assert.AreEqual(3, MedallionSchemaCatalog.CanonicalViews.Count, "Phase 1A should expose exactly three Golden contracts.");
    }

    [TestMethod]
    public void MedallionSchemaCatalog_ActiveSurface_HasExpectedBronzeTables()
    {
        var names = MedallionSchemaCatalog.RawTables
            .Select(static table => table.QualifiedName)
            .OrderBy(static name => name)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "bronze.dns_server_event",
                "bronze.windows_security_event",
                "bronze.windows_sysmon_event"
            },
            names);
    }

    [TestMethod]
    public void MedallionSchemaCatalog_ActiveSurface_HasExpectedSilverParserViews()
    {
        var names = MedallionSchemaCatalog.ParserViews
            .Select(static view => view.QualifiedName)
            .OrderBy(static name => name)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "silver.v_dns_server_query_event",
                "silver.v_dns_windows_sysmon_eid22",
                "silver.v_networksession_windows_security_eid5156",
                "silver.v_networksession_windows_sysmon_eid3",
                "silver.v_processevent_windows_security_eid4688",
                "silver.v_processevent_windows_sysmon_eid1"
            },
            names);
    }

    [TestMethod]
    public void MedallionSchemaCatalog_ActiveSurface_HasExpectedGoldenViews()
    {
        var names = MedallionSchemaCatalog.CanonicalViews
            .Select(static view => view.QualifiedName)
            .OrderBy(static name => name)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "golden.Dns",
                "golden.NetworkSession",
                "golden.ProcessEvent"
            },
            names);
    }

    [TestMethod]
    public void MedallionSchemaCatalog_ActiveSurface_HasExpectedGoldenContributorGraph()
    {
        var contributorGraph = MedallionSchemaCatalog.CanonicalViews
            .ToDictionary(
                static view => view.QualifiedName,
                static view => view.ParserViews.OrderBy(static parserView => parserView).ToArray(),
                StringComparer.OrdinalIgnoreCase);

        CollectionAssert.AreEqual(
            new[]
            {
                "silver.v_dns_server_query_event",
                "silver.v_dns_windows_sysmon_eid22"
            },
            contributorGraph["golden.Dns"]);

        CollectionAssert.AreEqual(
            new[]
            {
                "silver.v_networksession_windows_security_eid5156",
                "silver.v_networksession_windows_sysmon_eid3"
            },
            contributorGraph["golden.NetworkSession"]);

        CollectionAssert.AreEqual(
            new[]
            {
                "silver.v_processevent_windows_security_eid4688",
                "silver.v_processevent_windows_sysmon_eid1"
            },
            contributorGraph["golden.ProcessEvent"]);
    }

    [TestMethod]
    public void MedallionSchemaCatalog_ActiveSurface_HasExpectedSilverSourceGraph()
    {
        var sourceGraph = MedallionSchemaCatalog.ParserViews
            .ToDictionary(
                static parser => parser.QualifiedName,
                static parser => parser.Mapping.SourceObject,
                StringComparer.OrdinalIgnoreCase);

        Assert.AreEqual("bronze.windows_sysmon_event", sourceGraph["silver.v_processevent_windows_sysmon_eid1"]);
        Assert.AreEqual("bronze.windows_security_event", sourceGraph["silver.v_processevent_windows_security_eid4688"]);
        Assert.AreEqual("bronze.windows_sysmon_event", sourceGraph["silver.v_networksession_windows_sysmon_eid3"]);
        Assert.AreEqual("bronze.windows_security_event", sourceGraph["silver.v_networksession_windows_security_eid5156"]);
        Assert.AreEqual("bronze.windows_sysmon_event", sourceGraph["silver.v_dns_windows_sysmon_eid22"]);
        Assert.AreEqual("bronze.dns_server_event", sourceGraph["silver.v_dns_server_query_event"]);
    }

    [TestMethod]
    public void MedallionSchemaCatalog_ActiveSurface_DoesNotExposeLegacyNames()
    {
        var allObjectNames = MedallionSchemaCatalog.RawTables.Select(static table => table.Name)
            .Concat(MedallionSchemaCatalog.ParserViews.Select(static view => view.Name))
            .Concat(MedallionSchemaCatalog.CanonicalViews.Select(static view => view.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.IsFalse(allObjectNames.Contains("windows_event_json"));
        Assert.IsFalse(allObjectNames.Contains("ProcessEvents"));
        Assert.IsFalse(allObjectNames.Contains("NetworkSessions"));
        Assert.IsFalse(allObjectNames.Contains("DeviceProcessEvents"));
        Assert.IsFalse(allObjectNames.Contains("DeviceNetworkEvents"));
    }

    [TestMethod]
    public void MedallionSchemaCatalog_ActiveSurface_GoldenContractsUseExpectedSchemaAndNames()
    {
        var names = MedallionSchemaCatalog.CanonicalViews
            .Select(static view => view.Name)
            .OrderBy(static name => name)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "Dns",
                "NetworkSession",
                "ProcessEvent"
            },
            names);

        foreach (var view in MedallionSchemaCatalog.CanonicalViews)
        {
            Assert.AreEqual("golden", view.Schema, $"{view.QualifiedName} should live in the lowercase golden schema.");
            Assert.IsTrue(char.IsUpper(view.Name[0]), $"{view.QualifiedName} should use PascalCase table/view naming.");
        }
    }

    [TestMethod]
    public void MedallionSchemaCatalog_ActiveSurface_DoesNotExposeKnownLegacyPluralGoldenNames()
    {
        var names = MedallionSchemaCatalog.CanonicalViews
            .Select(static view => view.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.IsFalse(names.Contains("DnsEvents"));
        Assert.IsFalse(names.Contains("NetworkSessions"));
        Assert.IsFalse(names.Contains("ProcessEvents"));
        Assert.IsFalse(names.Contains("DeviceNetworkEvents"));
        Assert.IsFalse(names.Contains("DeviceProcessEvents"));
    }

    [TestMethod]
    public void MedallionSchemaCatalog_ActiveSurface_SilverViewsTargetExistingGoldenContracts()
    {
        var goldenNames = MedallionSchemaCatalog.CanonicalViews
            .Select(static view => view.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var parser in MedallionSchemaCatalog.ParserViews)
        {
            Assert.IsTrue(
                goldenNames.Contains(parser.CanonicalTarget),
                $"{parser.QualifiedName} targets missing Golden contract {parser.CanonicalTarget}.");
        }
    }

    [TestMethod]
    public void MedallionSchemaCatalog_ActiveSurface_SilverViewsUseExistingBronzeSources()
    {
        var bronzeNames = MedallionSchemaCatalog.RawTables
            .Select(static table => table.QualifiedName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var parser in MedallionSchemaCatalog.ParserViews)
        {
            Assert.IsTrue(
                bronzeNames.Contains(parser.Mapping.SourceObject),
                $"{parser.QualifiedName} reads from missing Bronze source {parser.Mapping.SourceObject}.");
        }
    }
}
