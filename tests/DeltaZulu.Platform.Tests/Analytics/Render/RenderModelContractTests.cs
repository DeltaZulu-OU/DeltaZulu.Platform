
using DeltaZulu.Platform.Domain.Analytics.Rendering;

namespace DeltaZulu.Platform.Tests.Analytics.Render;
[TestClass]
public sealed class RenderModelContractTests
{
    [TestMethod]
    public void RenderDirective_TableWithoutReason_IsNotFallback()
    {
        var directive = RenderDirective.Table();

        Assert.AreEqual(RenderKind.Table, directive.Kind);
        Assert.IsFalse(directive.IsFallback);
        Assert.IsNull(directive.FallbackReason);
    }

    [TestMethod]
    public void RenderDirective_TableWithReason_IsFallback()
    {
        var directive = RenderDirective.Table("No numeric Y column.");

        Assert.AreEqual(RenderKind.Table, directive.Kind);
        Assert.IsTrue(directive.IsFallback);
        Assert.AreEqual("No numeric Y column.", directive.FallbackReason);
    }

    [TestMethod]
    public void RenderSeries_DoesNotCarryWebColor()
    {
        var series = new RenderSeries("LaunchCount", [1, 2, 3]);

        Assert.AreEqual("LaunchCount", series.Name);
        CollectionAssert.AreEqual(new double[] { 1, 2, 3 }, series.Values.ToArray());
    }

    [TestMethod]
    public void RenderDiagnostic_CarriesSeverityAndMessage()
    {
        var diagnostic = new RenderDiagnostic(RenderDiagnosticSeverity.Warning, "Fallback to table.");

        Assert.AreEqual(RenderDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.AreEqual("Fallback to table.", diagnostic.Message);
    }
}