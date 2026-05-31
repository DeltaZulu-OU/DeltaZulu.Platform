namespace Hunting.Tests.Schema;

using Hunting.Core.Schema;
using Hunting.Schema;
using Hunting.Schema.Definitions.Phase1D;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class Phase1DActiveParserSpecCatalogTests
{
    private static readonly string[] ExpectedParserSpecs =
    [
        "silver.v_dns_server_query_event",
        "silver.v_dns_windows_sysmon_eid22",
        "silver.v_networksession_windows_security_eid5156",
        "silver.v_networksession_windows_sysmon_eid3",
        "silver.v_processevent_windows_security_eid4688",
        "silver.v_processevent_windows_sysmon_eid1"
    ];

    [TestMethod]
    public void ParserSpecCatalog_AllSpecsHaveReviewableProjectionCoverage()
    {
        foreach (var spec in Phase1DParserSpecCatalog.ParserSpecs)
        {
            Assert.IsNotEmpty(spec.Projections, spec.QualifiedName);
            Assert.IsTrue(spec.Projections.All(static projection => projection.Expression.StartsWith("existing:", StringComparison.Ordinal)));
            Assert.IsTrue(spec.Projections.All(static projection => projection.SourceField is not null));
        }
    }

    [TestMethod]
    public void ParserSpecCatalog_ExposesOneSpecPerActiveParserView()
    {
        var specs = Phase1DParserSpecCatalog.ParserSpecs;

        CollectionAssert.AreEqual(
            ExpectedParserSpecs,
            specs.Select(static spec => spec.QualifiedName).ToArray());

        CollectionAssert.AreEquivalent(
            SchemaConventions.ParserViews.Select(static view => view.QualifiedName).ToArray(),
            specs.Select(static spec => spec.QualifiedName).ToArray());
    }

    [TestMethod]
    public void ParserSpecCatalog_HasNoDuplicateSpecNames()
    {
        var duplicate = Phase1DParserSpecCatalog.ParserSpecs
            .GroupBy(static spec => spec.QualifiedName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);

        Assert.IsNull(duplicate);
    }

    [TestMethod]
    public void ParserSpecCatalog_HasNoLegacySourceObjects()
    {
        foreach (var spec in Phase1DParserSpecCatalog.ParserSpecs)
        {
            Assert.DoesNotContain("windows_event_json", spec.SourceObject);
            Assert.DoesNotContain("DeviceProcessEvents", spec.TargetContract);
            Assert.DoesNotContain("DeviceNetworkEvents", spec.TargetContract);
            Assert.DoesNotContain("ProcessEvents", spec.TargetContract);
            Assert.DoesNotContain("NetworkSessions", spec.TargetContract);
        }
    }

    [TestMethod]
    public void ParserSpecCatalog_TargetsActiveGoldenContracts()
    {
        var specs = Phase1DParserSpecCatalog.ParserSpecs
            .ToDictionary(static spec => spec.QualifiedName, StringComparer.OrdinalIgnoreCase);

        Assert.AreEqual("golden.ProcessEvent", specs["silver.v_processevent_windows_sysmon_eid1"].TargetContract);
        Assert.AreEqual("golden.ProcessEvent", specs["silver.v_processevent_windows_security_eid4688"].TargetContract);
        Assert.AreEqual("golden.NetworkSession", specs["silver.v_networksession_windows_sysmon_eid3"].TargetContract);
        Assert.AreEqual("golden.NetworkSession", specs["silver.v_networksession_windows_security_eid5156"].TargetContract);
        Assert.AreEqual("golden.Dns", specs["silver.v_dns_windows_sysmon_eid22"].TargetContract);
        Assert.AreEqual("golden.Dns", specs["silver.v_dns_server_query_event"].TargetContract);
    }

    [TestMethod]
    public void ParserSpecCatalog_UsesActiveSourceObjects()
    {
        var specs = Phase1DParserSpecCatalog.ParserSpecs
            .ToDictionary(static spec => spec.QualifiedName, StringComparer.OrdinalIgnoreCase);

        Assert.AreEqual("bronze.windows_sysmon_event", specs["silver.v_processevent_windows_sysmon_eid1"].SourceObject);
        Assert.AreEqual("bronze.windows_security_event", specs["silver.v_processevent_windows_security_eid4688"].SourceObject);
        Assert.AreEqual("bronze.windows_sysmon_event", specs["silver.v_networksession_windows_sysmon_eid3"].SourceObject);
        Assert.AreEqual("bronze.windows_security_event", specs["silver.v_networksession_windows_security_eid5156"].SourceObject);
        Assert.AreEqual("bronze.windows_sysmon_event", specs["silver.v_dns_windows_sysmon_eid22"].SourceObject);
        Assert.AreEqual("bronze.dns_server_event", specs["silver.v_dns_server_query_event"].SourceObject);
    }

    [TestMethod]
    public void ParserSpecCatalog_UsesAdditionalFieldsPolicyOnlyWhenTargetSupportsAdditionalFields()
    {
        foreach (var spec in Phase1DParserSpecCatalog.ParserSpecs)
        {
            var target = SchemaConventions.CanonicalViews.Single(view =>
                view.QualifiedName.Equals(spec.TargetContract, StringComparison.OrdinalIgnoreCase));

            var targetHasAdditionalFields = target.Columns.Any(static column =>
                column.Name.Equals("AdditionalFields", StringComparison.OrdinalIgnoreCase));

            if (targetHasAdditionalFields)
            {
                Assert.AreEqual(ParserAdditionalFieldsPolicy.PreserveRawLog, spec.AdditionalFieldsPolicy, spec.QualifiedName);
            }
            else
            {
                Assert.AreEqual(ParserAdditionalFieldsPolicy.None, spec.AdditionalFieldsPolicy, spec.QualifiedName);
            }
        }
    }

    [TestMethod]
    public void ParserSpecCatalog_ValidatesEverySpecAgainstItsTargetContract()
    {
        foreach (var spec in Phase1DParserSpecCatalog.ParserSpecs)
        {
            var target = SchemaConventions.CanonicalViews.Single(view =>
                view.QualifiedName.Equals(spec.TargetContract, StringComparison.OrdinalIgnoreCase));

            var issues = ParserSpecValidator.ValidateAgainstTarget(spec, target);

            Assert.DoesNotContain(
                static issue => issue.Severity == ParserSpecValidationSeverity.Error, issues,
                $"{spec.QualifiedName} should validate against {target.QualifiedName}: {string.Join("; ", issues.Select(static issue => issue.Message))}");
        }
    }
}