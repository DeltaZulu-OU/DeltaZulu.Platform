
using DeltaZulu.Platform.Application.Hunting.Rendering.Services;
using DeltaZulu.Platform.Application.Hunting.Rendering.Tabular;
using DeltaZulu.Platform.Domain.Hunting.Rendering;

namespace DeltaZulu.Platform.Tests.Hunting.Render;
[TestClass]
public sealed class RenderResolverTests
{
    private readonly RenderResolver _resolver = new();

    [TestMethod]
    public void Resolve_TableDirective_ReturnsTablePlan()
    {
        var directive = RenderDirective.Table("Already table.") with { Title = "Raw rows" };

        var plan = _resolver.Resolve(directive, Columns());

        Assert.AreEqual(RenderKind.Table, plan.Kind);
        Assert.AreEqual("Raw rows", plan.Title);
        Assert.IsNull(plan.XColumn);
        Assert.IsEmpty(plan.YColumns);
        Assert.IsTrue(plan.IsFallback);
        Assert.AreEqual("Already table.", plan.FallbackReason);
    }

    [TestMethod]
    public void Resolve_ExplicitXColumn_ResolvesCaseInsensitively()
    {
        var directive = ChartDirective(xColumn: "timestamp", yColumns: ["LaunchCount"]);

        var plan = _resolver.Resolve(directive, Columns());

        Assert.IsFalse(plan.IsFallback);
        Assert.AreEqual("Timestamp", plan.XColumn);
    }

    [TestMethod]
    public void Resolve_ExplicitNumericYColumns_ResolveCaseInsensitively()
    {
        var directive = ChartDirective(xColumn: "Timestamp", yColumns: ["launchcount", "ProcessId"]);

        var plan = _resolver.Resolve(directive, Columns());

        Assert.IsFalse(plan.IsFallback);
        CollectionAssert.AreEqual(new[] { "LaunchCount", "ProcessId" }, plan.YColumns.ToArray());
    }

    [TestMethod]
    public void Resolve_ExplicitNonNumericYColumns_AreIgnored()
    {
        var directive = ChartDirective(xColumn: "Timestamp", yColumns: ["FileName", "LaunchCount"]);

        var plan = _resolver.Resolve(directive, Columns());

        Assert.IsFalse(plan.IsFallback);
        CollectionAssert.AreEqual(new[] { "LaunchCount" }, plan.YColumns.ToArray());
    }

    [TestMethod]
    public void Resolve_OnlyNonNumericExplicitYColumns_FallsBackToTable()
    {
        var directive = ChartDirective(xColumn: "Timestamp", yColumns: ["FileName"]);

        var plan = _resolver.Resolve(directive, Columns());

        Assert.IsTrue(plan.IsFallback);
        Assert.AreEqual(RenderKind.Table, plan.Kind);
        Assert.AreEqual("Unable to resolve ycolumns for render.", plan.FallbackReason);
    }

    [TestMethod]
    public void Resolve_MissingXColumn_UsesFirstTemporalColumn()
    {
        var directive = ChartDirective(yColumns: ["LaunchCount"]);

        var plan = _resolver.Resolve(directive, Columns());

        Assert.IsFalse(plan.IsFallback);
        Assert.AreEqual("Timestamp", plan.XColumn);
    }

    [TestMethod]
    public void Resolve_MissingXColumnWithoutTemporal_UsesFirstColumn()
    {
        var directive = ChartDirective(yColumns: ["LaunchCount"]);

        var plan = _resolver.Resolve(directive, ColumnsWithoutTemporal());

        Assert.IsFalse(plan.IsFallback);
        Assert.AreEqual("AccountName", plan.XColumn);
    }

    [TestMethod]
    public void Resolve_MissingYColumns_UsesFirstNumericColumn()
    {
        var directive = ChartDirective(xColumn: "AccountName");

        var plan = _resolver.Resolve(directive, Columns());

        Assert.IsFalse(plan.IsFallback);
        CollectionAssert.AreEqual(new[] { "LaunchCount" }, plan.YColumns.ToArray());
    }

    [TestMethod]
    public void Resolve_MissingYColumnsWithoutNumeric_FallsBackToTable()
    {
        var directive = ChartDirective(xColumn: "AccountName");

        var plan = _resolver.Resolve(directive, ColumnsWithoutNumeric());

        Assert.IsTrue(plan.IsFallback);
        Assert.AreEqual(RenderKind.Table, plan.Kind);
        Assert.AreEqual("Unable to resolve ycolumns for render.", plan.FallbackReason);
    }

    [TestMethod]
    public void Resolve_NoColumns_FallsBackToTable()
    {
        var directive = ChartDirective(yColumns: ["LaunchCount"]);

        var plan = _resolver.Resolve(directive, []);

        Assert.IsTrue(plan.IsFallback);
        Assert.AreEqual(RenderKind.Table, plan.Kind);
        Assert.AreEqual("Unable to resolve xcolumn for render.", plan.FallbackReason);
    }

    [TestMethod]
    public void Resolve_SeriesColumn_ResolvesCaseInsensitively()
    {
        var directive = ChartDirective(xColumn: "Timestamp", yColumns: ["LaunchCount"], seriesColumn: "accountname");

        var plan = _resolver.Resolve(directive, Columns());

        Assert.IsFalse(plan.IsFallback);
        Assert.AreEqual("AccountName", plan.SeriesColumn);
    }

    [TestMethod]
    public void Resolve_InvalidSeriesColumn_IsIgnored()
    {
        var directive = ChartDirective(xColumn: "Timestamp", yColumns: ["LaunchCount"], seriesColumn: "MissingColumn");

        var plan = _resolver.Resolve(directive, Columns());

        Assert.IsFalse(plan.IsFallback);
        Assert.IsNull(plan.SeriesColumn);
    }

    [TestMethod]
    public void Resolve_CarriesDirectiveMetadataToPlan()
    {
        var directive = ChartDirective(
            xColumn: "Timestamp",
            yColumns: ["LaunchCount"],
            title: "Launches",
            legend: "hidden",
            isStacked: true);

        var plan = _resolver.Resolve(directive, Columns());

        Assert.AreEqual(RenderKind.Barchart, plan.Kind);
        Assert.AreEqual("Launches", plan.Title);
        Assert.AreEqual("hidden", plan.Legend);
        Assert.IsTrue(plan.IsStacked);
    }

    private static RenderDirective ChartDirective(
        string? xColumn = null,
        IReadOnlyList<string>? yColumns = null,
        string? seriesColumn = null,
        string? title = null,
        string? legend = null,
        bool isStacked = false)
        => new()
        {
            Kind = RenderKind.Barchart,
            Title = title,
            Binding = new RenderBinding
            {
                XColumn = xColumn,
                YColumns = yColumns ?? [],
                SeriesColumn = seriesColumn
            },
            Legend = legend,
            IsStacked = isStacked
        };

    private static IReadOnlyList<RenderColumn> Columns()
        =>
        [
            new RenderColumn { Name = "Timestamp", TypeName = "TIMESTAMP", IsTemporal = true },
            new RenderColumn { Name = "AccountName", TypeName = "VARCHAR", IsCategorical = true },
            new RenderColumn { Name = "LaunchCount", TypeName = "BIGINT", IsNumeric = true },
            new RenderColumn { Name = "ProcessId", TypeName = "INTEGER", IsNumeric = true },
            new RenderColumn { Name = "FileName", TypeName = "VARCHAR", IsCategorical = true }
        ];

    private static IReadOnlyList<RenderColumn> ColumnsWithoutTemporal()
        =>
        [
            new RenderColumn { Name = "AccountName", TypeName = "VARCHAR", IsCategorical = true },
            new RenderColumn { Name = "LaunchCount", TypeName = "BIGINT", IsNumeric = true }
        ];

    private static IReadOnlyList<RenderColumn> ColumnsWithoutNumeric()
        =>
        [
            new RenderColumn { Name = "AccountName", TypeName = "VARCHAR", IsCategorical = true },
            new RenderColumn { Name = "FileName", TypeName = "VARCHAR", IsCategorical = true }
        ];
}