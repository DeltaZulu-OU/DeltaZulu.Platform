using DeltaZulu.Platform.Application.Analytics.Rendering.Services;
using DeltaZulu.Platform.Application.Analytics.Rendering.Tabular;
using DeltaZulu.Platform.Domain.Analytics.Rendering;

namespace DeltaZulu.Platform.Tests.Analytics.Render;

[TestClass]
public sealed class RenderChartBuilderTests
{
    private readonly RenderChartBuilder _builder = new();

    [TestMethod]
    public void Build_FailedInput_ReturnsFallbackChart()
    {
        var result = new RenderTabularResult { Success = false };

        var model = _builder.Build(result, Chart(RenderKind.Barchart));

        Assert.IsFalse(model.CanRender);
        Assert.AreEqual(RenderKind.Table, model.Kind);
        Assert.AreEqual("No render data.", model.Message);
    }

    [TestMethod]
    public void Build_TableDirective_ReturnsFallbackChart()
    {
        var model = _builder.Build(SamplePlainResult(), RenderDirective.Table());

        Assert.IsFalse(model.CanRender);
        Assert.AreEqual(RenderKind.Table, model.Kind);
        Assert.AreEqual("Render fell back to table.", model.Message);
    }

    [TestMethod]
    public void Build_PlainModel_BuildsXLabelsAndNumericSeries()
    {
        var directive = Chart(RenderKind.Barchart, xColumn: "AccountName", yColumns: ["LaunchCount"]);

        var model = _builder.Build(SamplePlainResult(), directive);

        Assert.IsTrue(model.CanRender);
        Assert.AreEqual(RenderKind.Barchart, model.Kind);
        CollectionAssert.AreEqual(new[] { "alice", "bob" }, model.XLabels.ToArray());
        Assert.AreEqual("LaunchCount", model.Series[0].Name);
        CollectionAssert.AreEqual(new[] { 3d, 5d }, model.Series[0].Values.ToArray());
        Assert.AreEqual(3d, model.YMin);
        Assert.AreEqual(5d, model.YMax);
    }

    [TestMethod]
    public void Build_MultipleYColumns_ProducesMultipleSeries()
    {
        var directive = Chart(RenderKind.Linechart, xColumn: "AccountName", yColumns: ["LaunchCount", "DeviceCount"]);

        var model = _builder.Build(SampleMultiMetricResult(), directive);

        Assert.IsTrue(model.CanRender);
        Assert.HasCount(2, model.Series);
        Assert.AreEqual("LaunchCount", model.Series[0].Name);
        Assert.AreEqual("DeviceCount", model.Series[1].Name);
        CollectionAssert.AreEqual(new[] { 3d, 5d }, model.Series[0].Values.ToArray());
        CollectionAssert.AreEqual(new[] { 1d, 2d }, model.Series[1].Values.ToArray());
    }

    [TestMethod]
    public void Build_GroupedModel_BuildsOneSeriesPerGroup()
    {
        var directive = Chart(RenderKind.Barchart, xColumn: "AccountName", yColumns: ["LaunchCount"], seriesColumn: "DeviceName");

        var model = _builder.Build(SampleGroupedResult(), directive);

        Assert.IsTrue(model.CanRender);
        CollectionAssert.AreEqual(new[] { "alice", "bob" }, model.XLabels.ToArray());
        Assert.HasCount(2, model.Series);
        Assert.AreEqual("host-a", model.Series[0].Name);
        Assert.AreEqual("host-b", model.Series[1].Name);
        CollectionAssert.AreEqual(new[] { 3d, 2d }, model.Series[0].Values.ToArray());
        CollectionAssert.AreEqual(new[] { 4d, 0d }, model.Series[1].Values.ToArray());
    }

    [TestMethod]
    public void Build_GroupedModel_WithMultipleYColumns_NamesSeriesByGroupAndMetric()
    {
        var directive = Chart(RenderKind.Columnchart, xColumn: "AccountName", yColumns: ["LaunchCount", "DeviceCount"], seriesColumn: "DeviceName");

        var model = _builder.Build(SampleGroupedMultiMetricResult(), directive);

        Assert.IsTrue(model.CanRender);
        Assert.HasCount(4, model.Series);
        Assert.AreEqual("host-a · LaunchCount", model.Series[0].Name);
        Assert.AreEqual("host-a · DeviceCount", model.Series[1].Name);
        Assert.AreEqual("host-b · LaunchCount", model.Series[2].Name);
        Assert.AreEqual("host-b · DeviceCount", model.Series[3].Name);
    }

    [TestMethod]
    public void Build_NullXLabelsBecomePlaceholder()
    {
        var result = new RenderTabularResult {
            Columns =
            [
                RenderTypeClassifier.Classify("AccountName", "VARCHAR"),
                RenderTypeClassifier.Classify("LaunchCount", "BIGINT")
            ],
            ColumnData =
            [
                [null],
                [1L]
            ],
            RowCount = 1
        };

        var model = _builder.Build(result, Chart(RenderKind.Barchart, xColumn: "AccountName", yColumns: ["LaunchCount"]));

        CollectionAssert.AreEqual(new[] { "(null)" }, model.XLabels.ToArray());
    }

    [TestMethod]
    public void Build_DateXLabelsUseStableFormat()
    {
        var result = new RenderTabularResult {
            Columns =
            [
                RenderTypeClassifier.Classify("Timestamp", "TIMESTAMP"),
                RenderTypeClassifier.Classify("LaunchCount", "BIGINT")
            ],
            ColumnData =
            [
                [new DateTime(2026, 6, 3, 14, 15, 16, DateTimeKind.Utc)],
                [1L]
            ],
            RowCount = 1
        };

        var model = _builder.Build(result, Chart(RenderKind.Linechart, xColumn: "Timestamp", yColumns: ["LaunchCount"]));

        CollectionAssert.AreEqual(new[] { "2026-06-03 14:15:16" }, model.XLabels.ToArray());
    }

    [TestMethod]
    public void Build_NumericConversion_HandlesSupportedValuesAndInvalidStrings()
    {
        var result = new RenderTabularResult {
            Columns =
            [
                RenderTypeClassifier.Classify("Label", "VARCHAR"),
                RenderTypeClassifier.Classify("Metric", "DOUBLE")
            ],
            ColumnData =
            [
                ["byte", "int", "long", "float", "double", "decimal", "numeric-string", "invalid", "null"],
                [(byte)1, 2, 3L, 4f, 5d, 6m, "7.5", "not-a-number", null]
            ],
            RowCount = 9
        };

        var model = _builder.Build(result, Chart(RenderKind.Linechart, xColumn: "Label", yColumns: ["Metric"]));

        CollectionAssert.AreEqual(new[] { 1d, 2d, 3d, 4d, 5d, 6d, 7.5d, 0d, 0d }, model.Series[0].Values.ToArray());
    }

    [TestMethod]
    public void Build_MissingNumericY_FallsBackToTable()
    {
        var result = new RenderTabularResult {
            Columns =
            [
                RenderTypeClassifier.Classify("AccountName", "VARCHAR"),
                RenderTypeClassifier.Classify("FileName", "VARCHAR")
            ],
            ColumnData =
            [
                ["alice"],
                ["powershell.exe"]
            ],
            RowCount = 1
        };

        var model = _builder.Build(result, Chart(RenderKind.Barchart, xColumn: "AccountName", yColumns: ["FileName"]));

        Assert.IsFalse(model.CanRender);
        Assert.AreEqual(RenderKind.Table, model.Kind);
        Assert.AreEqual("Unable to resolve ycolumns for render.", model.Message);
    }

    [TestMethod]
    public void Build_MoreThanMaxPoints_DownsamplesAndWarns()
    {
        const int inputPoints = 700;
        var result = new RenderTabularResult {
            Columns =
            [
                RenderTypeClassifier.Classify("Label", "VARCHAR"),
                RenderTypeClassifier.Classify("Metric", "DOUBLE")
            ],
            ColumnData =
            [
                Enumerable.Range(0, inputPoints).Select(i => (object?)$"L{i}").ToList(),
                Enumerable.Range(0, inputPoints).Select(i => (object?)(double)i).ToList()
            ],
            RowCount = inputPoints
        };

        var model = _builder.Build(result, Chart(RenderKind.Linechart, xColumn: "Label", yColumns: ["Metric"]));

        Assert.IsTrue(model.CanRender);
        Assert.HasCount(500, model.XLabels);
        Assert.HasCount(500, model.Series[0].Values);
        Assert.Contains("sampled to 500 points", model.Warning);
    }

    private static RenderTabularResult SamplePlainResult()
        => new() {
            Columns =
            [
                RenderTypeClassifier.Classify("AccountName", "VARCHAR"),
                RenderTypeClassifier.Classify("LaunchCount", "BIGINT")
            ],
            ColumnData =
            [
                ["alice", "bob"],
                [3L, 5L]
            ],
            RowCount = 2
        };

    private static RenderTabularResult SampleMultiMetricResult()
        => new() {
            Columns =
            [
                RenderTypeClassifier.Classify("AccountName", "VARCHAR"),
                RenderTypeClassifier.Classify("LaunchCount", "BIGINT"),
                RenderTypeClassifier.Classify("DeviceCount", "BIGINT")
            ],
            ColumnData =
            [
                ["alice", "bob"],
                [3L, 5L],
                [1L, 2L]
            ],
            RowCount = 2
        };

    private static RenderTabularResult SampleGroupedResult()
        => new() {
            Columns =
            [
                RenderTypeClassifier.Classify("AccountName", "VARCHAR"),
                RenderTypeClassifier.Classify("DeviceName", "VARCHAR"),
                RenderTypeClassifier.Classify("LaunchCount", "BIGINT")
            ],
            ColumnData =
            [
                ["alice", "alice", "bob"],
                ["host-a", "host-b", "host-a"],
                [3L, 4L, 2L]
            ],
            RowCount = 3
        };

    private static RenderTabularResult SampleGroupedMultiMetricResult()
        => new() {
            Columns =
            [
                RenderTypeClassifier.Classify("AccountName", "VARCHAR"),
                RenderTypeClassifier.Classify("DeviceName", "VARCHAR"),
                RenderTypeClassifier.Classify("LaunchCount", "BIGINT"),
                RenderTypeClassifier.Classify("DeviceCount", "BIGINT")
            ],
            ColumnData =
            [
                ["alice", "alice"],
                ["host-a", "host-b"],
                [3L, 4L],
                [1L, 2L]
            ],
            RowCount = 2
        };

    private static RenderDirective Chart(
        RenderKind kind,
        string? xColumn = null,
        IReadOnlyList<string>? yColumns = null,
        string? seriesColumn = null)
        => new() {
            Kind = kind,
            Binding = new RenderBinding {
                XColumn = xColumn,
                YColumns = yColumns ?? [],
                SeriesColumn = seriesColumn
            }
        };
}