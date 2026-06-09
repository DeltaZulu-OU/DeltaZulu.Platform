using DeltaZulu.Blazor.Components;

namespace Workbench.Tests.BlazorComponents;

[TestClass]
public sealed class DzMarkdownRendererTests
{
    [TestMethod]
    public void RenderToHtml_DoesNotRewriteExternalLinks()
    {
        var html = DzMarkdownRenderer.RenderToHtml("[External](https://example.invalid/path)");

        StringAssert.Contains(html, "href=\"https://example.invalid/path\"");
    }

    [TestMethod]
    public void RenderToHtml_RewritesRelativeLinksThroughMapper()
    {
        var html = DzMarkdownRenderer.RenderToHtml(
            "[Rule](../rule.kql)",
            currentDocumentPath: "detections/anomalous-sign-in/notes/investigation.md",
            linkMapper: path => $"/mapped/{path}");

        StringAssert.Contains(html, "href=\"/mapped/detections/anomalous-sign-in/rule.kql\"");
    }

    [TestMethod]
    public void RenderToHtml_EscapesRawHtmlByDefault()
    {
        var html = DzMarkdownRenderer.RenderToHtml("<script>alert('x')</script>");

        Assert.IsFalse(html.Contains("<script>", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(html, "script");
    }
}
