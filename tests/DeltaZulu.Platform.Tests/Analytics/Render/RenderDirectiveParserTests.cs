
using DeltaZulu.Platform.Application.Analytics.Render.Directives;
using DeltaZulu.Platform.Domain.Analytics.Rendering;

namespace DeltaZulu.Platform.Tests.Analytics.Render;
[TestClass]
public sealed class RenderDirectiveParserTests
{
    private readonly RenderDirectiveParser _parser = new();

    [TestMethod]
    public void Parse_NoRenderDirective_ReturnsOriginalQueryAndTableDirectiveWithoutRenderIntent()
    {
        var result = _parser.Parse("ProcessEvent | take 10");

        Assert.AreEqual("ProcessEvent | take 10", result.QueryTextWithoutRender);
        Assert.AreEqual(RenderKind.Table, result.Directive.Kind);
        Assert.IsFalse(result.HasRenderDirective);
        Assert.IsFalse(result.Directive.IsFallback);
        Assert.IsNull(result.Directive.FallbackReason);
    }

    [TestMethod]
    public void Parse_BareRenderDirective_StripsDirectiveAndMapsToTable()
    {
        var result = _parser.Parse("ProcessEvent | take 10 | render");

        Assert.AreEqual("ProcessEvent | take 10", result.QueryTextWithoutRender);
        Assert.AreEqual(RenderKind.Table, result.Directive.Kind);
        Assert.IsTrue(result.HasRenderDirective);
        Assert.IsFalse(result.Directive.IsFallback);
        Assert.IsNull(result.Directive.FallbackReason);
    }

    [TestMethod]
    public void Parse_TerminalRenderDirective_StripsDirectiveAndMapsKind()
    {
        var result = _parser.Parse("ProcessEvent | summarize Count = count() by AccountName | render barchart");

        Assert.AreEqual("ProcessEvent | summarize Count = count() by AccountName", result.QueryTextWithoutRender);
        Assert.AreEqual(RenderKind.Barchart, result.Directive.Kind);
        Assert.IsTrue(result.HasRenderDirective);
        Assert.IsFalse(result.Directive.IsFallback);
    }

    [TestMethod]
    public void Parse_LegacyProperties_MapsBindingAndTitle()
    {
        var result = _parser.Parse("ProcessEvent | summarize LaunchCount = count() by AccountName | render barchart xcolumn=AccountName ycolumns=LaunchCount title=\"Launches by account\" series=DeviceName legend=hidden kind=stacked");

        Assert.AreEqual(RenderKind.Barchart, result.Directive.Kind);
        Assert.IsTrue(result.HasRenderDirective);
        Assert.AreEqual("Launches by account", result.Directive.Title);
        Assert.AreEqual("AccountName", result.Directive.Binding.XColumn);
        CollectionAssert.AreEqual(new[] { "LaunchCount" }, result.Directive.Binding.YColumns.ToArray());
        Assert.AreEqual("DeviceName", result.Directive.Binding.SeriesColumn);
        Assert.AreEqual("hidden", result.Directive.Legend);
        Assert.IsTrue(result.Directive.IsStacked);
    }

    [TestMethod]
    public void Parse_WithProperties_MapsQuotedAndCsvValues()
    {
        var result = _parser.Parse("ProcessEvent | summarize A = count(), B = countif(AccountName != '') by DeviceName | render columnchart with (xcolumn=DeviceName, ycolumns=A,B, title='Device counts')");

        Assert.AreEqual(RenderKind.Columnchart, result.Directive.Kind);
        Assert.IsTrue(result.HasRenderDirective);
        Assert.AreEqual("Device counts", result.Directive.Title);
        Assert.AreEqual("DeviceName", result.Directive.Binding.XColumn);
        CollectionAssert.AreEqual(new[] { "A", "B" }, result.Directive.Binding.YColumns.ToArray());
    }

    [TestMethod]
    public void Parse_UnsupportedKind_StripsDirectiveAndReturnsFallbackTable()
    {
        var result = _parser.Parse("ProcessEvent | take 10 | render heatmap");

        Assert.AreEqual("ProcessEvent | take 10", result.QueryTextWithoutRender);
        Assert.AreEqual(RenderKind.Table, result.Directive.Kind);
        Assert.IsTrue(result.HasRenderDirective);
        Assert.IsTrue(result.Directive.IsFallback);
        Assert.AreEqual("Unsupported render kind 'heatmap'.", result.Directive.FallbackReason);
    }

    [TestMethod]
    public void Parse_MalformedProperty_StripsDirectiveAndReturnsFallbackTable()
    {
        var result = _parser.Parse("ProcessEvent | take 10 | render barchart xcolumn");

        Assert.AreEqual("ProcessEvent | take 10", result.QueryTextWithoutRender);
        Assert.AreEqual(RenderKind.Table, result.Directive.Kind);
        Assert.IsTrue(result.HasRenderDirective);
        Assert.IsTrue(result.Directive.IsFallback);
        Assert.AreEqual("Malformed render property 'xcolumn'. Expected key=value.", result.Directive.FallbackReason);
    }

    [TestMethod]
    public void Parse_NonTerminalRenderToken_ReturnsOriginalQueryAndFallbackTableWithRenderIntent()
    {
        const string query = "ProcessEvent | render barchart | take 10";

        var result = _parser.Parse(query);

        Assert.AreEqual(query, result.QueryTextWithoutRender);
        Assert.AreEqual(RenderKind.Table, result.Directive.Kind);
        Assert.IsTrue(result.HasRenderDirective);
        Assert.IsTrue(result.Directive.IsFallback);
        Assert.AreEqual("Render clause must be terminal and use key=value properties.", result.Directive.FallbackReason);
    }

    [TestMethod]
    public void Parse_TrailingSemicolonAndWhitespace_StripsDirective()
    {
        var result = _parser.Parse("ProcessEvent | take 10 | render table;   ");

        Assert.AreEqual("ProcessEvent | take 10", result.QueryTextWithoutRender);
        Assert.AreEqual(RenderKind.Table, result.Directive.Kind);
        Assert.IsTrue(result.HasRenderDirective);
        Assert.IsFalse(result.Directive.IsFallback);
    }
}