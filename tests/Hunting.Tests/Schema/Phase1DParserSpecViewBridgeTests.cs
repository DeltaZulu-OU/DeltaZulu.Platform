namespace Hunting.Tests.Schema;

using Hunting.Core.Schema;
using Hunting.Schema;
using Hunting.Schema.Definitions.Phase1D;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class Phase1DParserSpecViewBridgeTests
{
    [TestMethod]
    public void ParserSpecViewBridge_ActiveSpecsDescribeExistingParserViews()
    {
        foreach (var spec in Phase1DParserSpecCatalog.ParserSpecs)
        {
            var parserView = FindParserView(spec);
            var target = FindTarget(spec);

            var issues = ParserSpecViewBridge.Validate(spec, parserView, target);

            Assert.DoesNotContain(
                static issue => issue.Severity == ParserSpecValidationSeverity.Error, issues,
                $"{spec.QualifiedName}: {string.Join("; ", issues.Select(static issue => issue.Message))}");
        }
    }

    [TestMethod]
    public void ParserSpecViewBridge_FailsWhenParserViewMapsColumnNotSuppliedBySpec()
    {
        var spec = Phase1DParserSpecCatalog.ParserSpecs[0];
        var changed = CloneParserSpec(
            spec,
            projections: spec.Projections.Take(1).ToArray(),
            intentionalNulls: []);

        var issues = ParserSpecViewBridge.Validate(changed, FindParserView(spec), FindTarget(spec));

        Assert.Contains(static issue =>
            issue.Message.Contains("neither projects it nor marks it intentionally null", StringComparison.OrdinalIgnoreCase), issues);
    }

    [TestMethod]
    public void ParserSpecViewBridge_FailsWhenProjectionIsNotMappedByParserView()
    {
        var spec = Phase1DParserSpecCatalog.ParserSpecs[0];
        var changed = CloneParserSpec(
            spec,
            projections:
            [
                new ParserProjectionSpec("UnknownColumn", "existing:UnknownColumn")
            ],
            intentionalNulls: spec.IntentionalNulls);

        var issues = ParserSpecViewBridge.Validate(changed, FindParserView(spec), FindTarget(spec));

        Assert.Contains(static issue =>
            issue.Message.Contains("is not exposed by parser view", StringComparison.OrdinalIgnoreCase) ||
            issue.Message.Contains("is not mapped by parser view", StringComparison.OrdinalIgnoreCase), issues);
    }

    [TestMethod]
    public void ParserSpecViewBridge_FailsWhenSourceNameDoesNotMatchParserView()
    {
        var spec = Phase1DParserSpecCatalog.ParserSpecs[0];
        var changed = CloneParserSpec(spec, sourceName: "Wrong Source");

        var issues = ParserSpecViewBridge.Validate(changed, FindParserView(spec), FindTarget(spec));

        Assert.Contains(static issue =>
            issue.Message.Contains("does not match parser view source name", StringComparison.OrdinalIgnoreCase), issues);
    }

    [TestMethod]
    public void ParserSpecViewBridge_FailsWhenSourceObjectDoesNotMatchParserView()
    {
        var spec = Phase1DParserSpecCatalog.ParserSpecs[0];
        var changed = CloneParserSpec(spec, sourceObject: "bronze.windows_event_json");

        var issues = ParserSpecViewBridge.Validate(changed, FindParserView(spec), FindTarget(spec));

        Assert.Contains(static issue =>
            issue.Message.Contains("does not match parser view source object", StringComparison.OrdinalIgnoreCase), issues);
    }

    [TestMethod]
    public void ParserSpecViewBridge_FailsWhenSpecNameDoesNotMatchParserView()
    {
        var spec = Phase1DParserSpecCatalog.ParserSpecs[0];
        var changed = CloneParserSpec(spec, name: "silver.v_wrong_name");

        var issues = ParserSpecViewBridge.Validate(changed, FindParserView(spec), FindTarget(spec));

        Assert.Contains(static issue =>
            issue.Message.Contains("does not describe parser view", StringComparison.OrdinalIgnoreCase), issues);
    }

    [TestMethod]
    public void ParserSpecViewBridge_FailsWhenTargetDoesNotMatchParserView()
    {
        var spec = Phase1DParserSpecCatalog.ParserSpecs[0];
        var changed = CloneParserSpec(spec, targetContract: "golden.ProcessEvents");

        var issues = ParserSpecViewBridge.Validate(changed, FindParserView(spec), FindTarget(spec));

        Assert.Contains(static issue =>
            issue.Message.Contains("does not match parser view target", StringComparison.OrdinalIgnoreCase), issues);
    }

    [TestMethod]
    public void ParserSpecViewBridge_ThrowsWhenBridgeHasErrors()
    {
        var spec = Phase1DParserSpecCatalog.ParserSpecs[0];
        var changed = CloneParserSpec(spec, sourceObject: "bronze.windows_event_json");

        var ex = Assert.ThrowsExactly<ParserSpecViewBridgeException>(() =>
            ParserSpecViewBridge.ToExistingParserView(changed, FindParserView(spec), FindTarget(spec)));

        Assert.AreEqual(changed.QualifiedName, ex.ParserName);
        Assert.IsNotEmpty(ex.Errors);
    }

    [TestMethod]
    public void ParserSpecViewBridge_ToExistingParserViewReturnsExistingParserViewWhenSpecMatches()
    {
        var spec = Phase1DParserSpecCatalog.ParserSpecs[0];
        var parserView = FindParserView(spec);
        var target = FindTarget(spec);

        var bridged = ParserSpecViewBridge.ToExistingParserView(spec, parserView, target);

        Assert.AreSame(parserView, bridged);
    }

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
        new(
            name ?? original.Name,
            sourceObject ?? original.SourceObject,
            targetContract ?? original.TargetContract,
            sourceName ?? original.SourceName,
            selector ?? original.Selector,
            projections ?? original.Projections,
            intentionalNulls ?? original.IntentionalNulls,
            additionalFieldsPolicy ?? original.AdditionalFieldsPolicy);

    private static ParserViewDef FindParserView(ParserSpec spec) =>
            SchemaConventions.ParserViews.Single(view =>
            view.QualifiedName.Equals(spec.QualifiedName, StringComparison.OrdinalIgnoreCase));

    private static CanonicalViewDef FindTarget(ParserSpec spec) =>
        SchemaConventions.CanonicalViews.Single(view =>
            view.QualifiedName.Equals(spec.TargetContract, StringComparison.OrdinalIgnoreCase) ||
            view.Name.Equals(spec.TargetContract, StringComparison.OrdinalIgnoreCase));
}