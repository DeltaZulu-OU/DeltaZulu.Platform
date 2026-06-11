namespace DeltaZulu.Platform.Tests.Hunting.Web;

using DeltaZulu.Platform.Web.Hunting.Dashboards.Markdown;

[TestClass]
public sealed class DashboardMarkdownRendererTests
{
    [TestMethod]
    public void ToHtml_UsesMarkdigForCommonMarkdownBlocks()
    {
        var html = DashboardMarkdownRenderer.ToHtml("""
# Hunt notes

- Review `powershell.exe`
- Check **new** hosts

> Escalate if confirmed
""");

        Assert.Contains("<h1", html);
        Assert.Contains("Hunt notes", html);
        Assert.Contains("<ul>", html);
        Assert.Contains("<li>Review <code>powershell.exe</code></li>", html);
        Assert.Contains("<li>Check <strong>new</strong> hosts</li>", html);
        Assert.Contains("<blockquote>", html);
        Assert.Contains("Escalate if confirmed", html);
    }

    [TestMethod]
    public void ToHtml_DisablesRawHtmlAndBlocksUnsafeLinks()
    {
        var html = DashboardMarkdownRenderer.ToHtml("""
<script>alert('x')</script>
[unsafe](javascript:alert('x')) [safe](https://example.test/hunt)
""");

        Assert.Contains("&lt;script", html);
        Assert.Contains("alert", html);
        Assert.DoesNotContain("javascript:alert", html);
        Assert.Contains("<a href=\"https://example.test/hunt\" target=\"_blank\" rel=\"noopener noreferrer\">safe</a>", html);
    }
}