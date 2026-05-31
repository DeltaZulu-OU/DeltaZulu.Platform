namespace Hunting.Tests.Schema;

using Hunting.Core.Schema;
using Hunting.Schema;
using Hunting.Schema.Definitions.Phase1D;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class Phase1DParserSpecCatalogValidationTests
{
    [TestMethod]
    public void ParserSpecCatalogValidator_ActiveCatalogHasNoErrors()
    {
        var issues = Phase1DParserSpecCatalogValidator.ValidateActiveCatalog();

        Assert.DoesNotContain(
            static issue => issue.Severity == ParserSpecValidationSeverity.Error, issues,
            string.Join(Environment.NewLine, issues.Select(static issue => $"{issue.ParserName}: {issue.Message}")));
    }

    [TestMethod]
    public void ParserSpecCatalogValidator_ActiveCatalogHasOneSpecPerParserView()
    {
        var issues = Phase1DParserSpecCatalogValidator.ValidateActiveCatalog();

        Assert.DoesNotContain(static issue => issue.Message.Contains("has no parser spec", StringComparison.OrdinalIgnoreCase), issues);
        Assert.DoesNotContain(static issue => issue.Message.Contains("has no active parser view", StringComparison.OrdinalIgnoreCase), issues);
    }

    [TestMethod]
    public void ParserSpecCatalogValidator_FailsWhenParserViewHasNoSpec()
    {
        var specs = Phase1DParserSpecCatalog.ParserSpecs
            .Where(static spec => spec.QualifiedName != "silver.v_dns_server_query_event")
            .ToArray();

        var issues = Phase1DParserSpecCatalogValidator.Validate(
            specs,
            SchemaConventions.RawTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews);

        Assert.Contains(static issue =>
            issue.ParserName == "silver.v_dns_server_query_event" &&
            issue.Message.Contains("has no parser spec", StringComparison.OrdinalIgnoreCase), issues);
    }

    [TestMethod]
    public void ParserSpecCatalogValidator_FailsWhenSpecHasNoActiveParserView()
    {
        var extra = CreateParserSpec(
            name: "silver.v_obsolete_parser",
            sourceObject: "bronze.windows_sysmon_event",
            targetContract: "golden.ProcessEvent",
            projections:
            [
                new ParserProjectionSpec("Timestamp", "existing:Timestamp")
            ]);

        var specs = Phase1DParserSpecCatalog.ParserSpecs
            .Concat([extra])
            .ToArray();

        var issues = Phase1DParserSpecCatalogValidator.Validate(
            specs,
            SchemaConventions.RawTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews);

        Assert.Contains(static issue =>
            issue.ParserName == "silver.v_obsolete_parser" &&
            issue.Message.Contains("has no active parser view", StringComparison.OrdinalIgnoreCase), issues);
    }

    [TestMethod]
    public void ParserSpecCatalogValidator_FailsWhenSourceObjectIsNotActiveBronzeTable()
    {
        var original = Phase1DParserSpecCatalog.ParserSpecs[0];
        var changed = CloneParserSpec(original, sourceObject: "bronze.windows_event_json");

        var specs = ReplaceSpec(original, changed);

        var issues = Phase1DParserSpecCatalogValidator.Validate(
            specs,
            SchemaConventions.RawTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews);

        Assert.Contains(static issue =>
            issue.Message.Contains("is not an active Bronze table", StringComparison.OrdinalIgnoreCase), issues);
    }

    [TestMethod]
    public void ParserSpecCatalogValidator_FailsWhenTargetContractIsNotActiveGoldenContract()
    {
        var original = Phase1DParserSpecCatalog.ParserSpecs[0];
        var changed = CloneParserSpec(original, targetContract: "golden.ProcessEvents");

        var specs = ReplaceSpec(original, changed);

        var issues = Phase1DParserSpecCatalogValidator.Validate(
            specs,
            SchemaConventions.RawTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews);

        Assert.Contains(static issue =>
            issue.Message.Contains("is not an active Golden contract", StringComparison.OrdinalIgnoreCase), issues);
    }

    [TestMethod]
    public void ParserSpecCatalogValidator_FailsWhenSpecDoesNotCoverTargetColumns()
    {
        var original = Phase1DParserSpecCatalog.ParserSpecs[0];
        var changed = CloneParserSpec(
            original,
            projections: original.Projections.Take(1).ToArray(),
            intentionalNulls: []);

        var specs = ReplaceSpec(original, changed);

        var issues = Phase1DParserSpecCatalogValidator.Validate(
            specs,
            SchemaConventions.RawTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews);

        Assert.Contains(static issue =>
            issue.Message.Contains("does not supply target column", StringComparison.OrdinalIgnoreCase), issues);
    }

    [TestMethod]
    public void ParserSpecCatalogValidator_WarnsWhenAdditionalFieldsPolicyAndTargetDisagree()
    {
        var original = Phase1DParserSpecCatalog.ParserSpecs.First(spec =>
            spec.TargetContract.Equals("golden.ProcessEvent", StringComparison.OrdinalIgnoreCase));

        var changed = CloneParserSpec(original, additionalFieldsPolicy: ParserAdditionalFieldsPolicy.None);

        var specs = ReplaceSpec(original, changed);

        var issues = Phase1DParserSpecCatalogValidator.Validate(
            specs,
            SchemaConventions.RawTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews);

        Assert.Contains(static issue =>
            issue.Severity == ParserSpecValidationSeverity.Warning &&
            issue.Message.Contains("does not preserve raw log fields", StringComparison.OrdinalIgnoreCase), issues);
    }

    [TestMethod]
    public void ParserSpecCatalogValidator_ReportsDeterministicIssueOrdering()
    {
        var original = Phase1DParserSpecCatalog.ParserSpecs[0];
        var changed = CloneParserSpec(
            original,
            sourceObject: "bronze.unknown",
            targetContract: "golden.Unknown");

        var specs = ReplaceSpec(original, changed);

        var first = Phase1DParserSpecCatalogValidator.Validate(
            specs,
            SchemaConventions.RawTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews);

        var second = Phase1DParserSpecCatalogValidator.Validate(
            specs,
            SchemaConventions.RawTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews);

        CollectionAssert.AreEqual(
            first.Select(static issue => $"{issue.ParserName}|{issue.ColumnName}|{issue.Message}").ToArray(),
            second.Select(static issue => $"{issue.ParserName}|{issue.ColumnName}|{issue.Message}").ToArray());
    }

    private static IReadOnlyList<ParserSpec> ReplaceSpec(ParserSpec original, ParserSpec changed) =>
        Phase1DParserSpecCatalog.ParserSpecs
            .Select(spec => spec.QualifiedName.Equals(original.QualifiedName, StringComparison.OrdinalIgnoreCase) ? changed : spec)
            .ToArray();

    private static ParserSpec CloneParserSpec(
        ParserSpec original,
        string? name = null,
        string? sourceObject = null,
        string? targetContract = null,
        string? sourceName = null,
        string? selector = null,
        IReadOnlyList<ParserProjectionSpec>? projections = null,
        IReadOnlyList<ParserIntentionalNullSpec>? intentionalNulls = null,
        ParserAdditionalFieldsPolicy? additionalFieldsPolicy = null) =>
        CreateParserSpec(
            name ?? original.Name,
            sourceObject ?? original.SourceObject,
            targetContract ?? original.TargetContract,
            sourceName ?? original.SourceName,
            selector ?? original.Selector,
            projections ?? original.Projections,
            intentionalNulls ?? original.IntentionalNulls,
            additionalFieldsPolicy ?? original.AdditionalFieldsPolicy);

    private static ParserSpec CreateParserSpec(
        string name,
        string sourceObject,
        string targetContract,
        string sourceName = "Windows Sysmon",
        string selector = "EventID = 1",
        IReadOnlyList<ParserProjectionSpec>? projections = null,
        IReadOnlyList<ParserIntentionalNullSpec>? intentionalNulls = null,
        ParserAdditionalFieldsPolicy additionalFieldsPolicy = ParserAdditionalFieldsPolicy.PreserveRawLog) =>
        new(
            name,
            sourceObject,
            targetContract,
            sourceName,
            selector,
            projections ?? [new ParserProjectionSpec("Timestamp", "existing:Timestamp")],
            intentionalNulls ?? [],
            additionalFieldsPolicy);
}