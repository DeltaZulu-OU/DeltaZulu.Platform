using Hunting.Core.Policy;
using Hunting.Core.Render;
using Hunting.Data;
using Hunting.Data.Render;

namespace Hunting.Tests.Translation;

[TestClass]
public sealed class RenderChartBuilderTests
{
    private readonly RenderChartBuilder _builder = new();

    [TestMethod]
    [Description("Null query result returns table fallback model with explanatory message")]
    public void Build_NullResult_ReturnsFallbackModel()
    {
        var model = _builder.Build(null);

        Assert.IsFalse(model.CanRender);
        Assert.AreEqual(RenderKind.Table, model.Kind);
        Assert.AreEqual("No render data.", model.Message);
    }

    [TestMethod]
    [Description("Large chart payload is downsampled to 500 points and emits warning")]
    public void Build_MoreThanMaxPoints_DownsamplesAndWarns()
    {
        const int inputPoints = 700;
        var labels = Enumerable.Range(0, inputPoints).Select(i => $"L{i}").ToList();
        var values = Enumerable.Range(0, inputPoints).Select(i => $"{(double)i}").ToList();

        var result = CreateResult(
            new RenderSpec(RenderKind.Linechart, null, "Label", ["Metric"], null, null, false, false, null),
            [
                new ResultColumn("Label", "VARCHAR"),
                new ResultColumn("Metric", "DOUBLE")
            ],
            labels,
            values);

        var model = _builder.Build(result);

        Assert.IsTrue(model.CanRender);
        Assert.HasCount(500, model.XLabels);
        Assert.HasCount(500, model.Series[0].Values);
        Assert.Contains("sampled to 500 points", model.Warning);
    }

    private static QueryResult CreateResult(RenderSpec renderSpec, IReadOnlyList<ResultColumn> columns, params IReadOnlyList<object?>[] columnData)
    {
        var mutable = columnData.Select(col => col.ToList()).ToArray();
        return QueryResult.FromData(columns.ToList(), mutable, null, null, null, null, new DiagnosticBag(), renderSpec);
    }
}
