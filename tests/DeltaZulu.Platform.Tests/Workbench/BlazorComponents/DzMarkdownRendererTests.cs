using DeltaZulu.Platform.Web.Components;

namespace DeltaZulu.Platform.Tests.Workbench.BlazorComponents;

[TestClass]
public sealed class DzMarkdownRendererTests
{
    [TestMethod]
    public void RenderToHtml_DoesNotRewriteExternalLinks()
    {
        var html = DzMarkdownRenderer.RenderToHtml("[External](https://example.invalid/path)");

        Assert.Contains("href=\"https://example.invalid/path\"", html);
    }

    [TestMethod]
    public void RenderToHtml_RewritesRelativeLinksThroughMapper()
    {
        var html = DzMarkdownRenderer.RenderToHtml(
            "[Rule](../rule.kql)",
            currentDocumentPath: "detections/anomalous-sign-in/notes/investigation.md",
            linkMapper: path => $"/mapped/{path}");

        Assert.Contains("href=\"/mapped/detections/anomalous-sign-in/rule.kql\"", html);
    }

    [TestMethod]
    public void RenderToHtml_EscapesRawHtmlByDefault()
    {
        var html = DzMarkdownRenderer.RenderToHtml("<script>alert('x')</script>");

        Assert.IsFalse(html.Contains("<script>", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("script", html);
    }
}