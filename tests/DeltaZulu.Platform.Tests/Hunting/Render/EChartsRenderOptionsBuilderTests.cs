namespace DeltaZulu.Platform.Tests.Hunting.Render;

using DeltaZulu.Platform.Domain.Hunting.Rendering;
using DeltaZulu.Platform.Web.Hunting.Rendering;

[TestClass]
public sealed class EChartsRenderOptionsBuilderTests
{
    [TestMethod]
    public void ShouldShowLegend_NoLegendDirective_ReturnsTrue()
    {
        var chart = CreateChart(legend: null);

        Assert.IsTrue(EChartsRenderOptionsBuilder.ShouldShowLegend(chart));
    }

    [TestMethod]
    [DataRow("hidden")]
    [DataRow("hide")]
    [DataRow("none")]
    [DataRow("off")]
    [DataRow(" HIDDEN ")]
    public void ShouldShowLegend_HiddenLegendDirective_ReturnsFalse(string legend)
    {
        var chart = CreateChart(legend);

        Assert.IsFalse(EChartsRenderOptionsBuilder.ShouldShowLegend(chart));
    }

    [TestMethod]
    public void Build_FallbackChart_ReturnsEmptyOptions()
    {
        var builder = new EChartsRenderOptionsBuilder();
        var options = builder.Build(new RenderChartModel(
            false,
            "Render fell back to table.",
            string.Empty,
            string.Empty,
            null,
            [],
            [],
            0,
            1,
            null,
            false,
            RenderKind.Table));

        Assert.IsNotNull(options);
    }

    private static RenderChartModel CreateChart(string? legend)
        => new(
            true,
            string.Empty,
            string.Empty,
            "AccountName",
            null,
            ["alice"],
            [new RenderSeries("LaunchCount", [3])],
            0,
            3,
            legend,
            false,
            RenderKind.Barchart);
}