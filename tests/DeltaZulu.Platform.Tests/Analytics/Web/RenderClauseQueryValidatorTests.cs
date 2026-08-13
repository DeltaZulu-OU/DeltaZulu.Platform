using DeltaZulu.Platform.Web.Analytics.Rendering;

namespace DeltaZulu.Platform.Tests.Analytics.Web;

[TestClass]
public sealed class RenderClauseQueryValidatorTests
{
    [TestMethod]
    public void TryAppendOrReplaceTerminalRender_QueryWithoutRender_AppendsRenderClause()
    {
        var result = RenderClauseQueryValidator.TryAppendOrReplaceTerminalRender(
            "ProcessEvent | take 10",
            "| render barchart",
            out var updatedQueryText,
            out var error);

        Assert.IsTrue(result, error);
        Assert.AreEqual($"ProcessEvent | take 10{Environment.NewLine}| render barchart", updatedQueryText);
    }

    [TestMethod]
    public void TryAppendOrReplaceTerminalRender_QueryWithTerminalRender_ReplacesRenderClause()
    {
        var result = RenderClauseQueryValidator.TryAppendOrReplaceTerminalRender(
            "ProcessEvent | take 10 | render table",
            "| render barchart",
            out var updatedQueryText,
            out var error);

        Assert.IsTrue(result, error);
        Assert.AreEqual($"ProcessEvent | take 10{Environment.NewLine}| render barchart", updatedQueryText);
    }

    [TestMethod]
    public void TryAppendOrReplaceTerminalRender_QueryWithNonTerminalRender_ReturnsError()
    {
        var result = RenderClauseQueryValidator.TryAppendOrReplaceTerminalRender(
            "ProcessEvent | render table | take 10",
            "| render barchart",
            out _,
            out var error);

        Assert.IsFalse(result);
        Assert.Contains("not the final pipeline operator", error, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void TryAppendOrReplaceTerminalRender_QueryWithMultipleRenderCommands_ReturnsError()
    {
        var result = RenderClauseQueryValidator.TryAppendOrReplaceTerminalRender(
            "ProcessEvent | render table | summarize Count = count() | render barchart",
            "| render columnchart",
            out _,
            out var error);

        Assert.IsFalse(result);
        Assert.Contains("multiple render commands", error, StringComparison.OrdinalIgnoreCase);
    }
}
