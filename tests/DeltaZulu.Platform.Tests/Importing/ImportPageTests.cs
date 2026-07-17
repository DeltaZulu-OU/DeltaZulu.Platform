using Bunit;
using DeltaZulu.Platform.Tests.Components;
using DeltaZulu.Platform.Web.Analytics.Pages;

namespace DeltaZulu.Platform.Tests.Importing;

[TestClass]
public sealed class ImportPageTests
{
    [TestMethod]
    public async Task ImportPage_RendersBulkImportWorkflowAndDemoSeedAction()
    {
        await using var context = MudBlazorTestContext.Create();

        var cut = context.Render<Import>();

        Assert.Contains("Bulk import logs", cut.Markup);
        Assert.Contains("Source file or directory", cut.Markup);
        Assert.Contains("Use demo seed", cut.Markup);
        Assert.Contains("Run import preview", cut.Markup);
    }

    [TestMethod]
    public async Task ImportPage_UseDemoSeedThenRunImport_RendersSourceObjectsInDzDataTable()
    {
        await using var context = MudBlazorTestContext.Create();

        var cut = context.Render<Import>();

        FindButtonByText(cut, "Use demo seed").Click();
        FindButtonByText(cut, "Run import preview").Click();

        // The migrated source-objects table renders through DzDataTable/DzTableShell.
        var shell = cut.Find(".dz-table-shell");
        Assert.Contains("Source", shell.TextContent);
        Assert.Contains("SHA256", shell.TextContent);
        Assert.Contains("demo-seed-preview", cut.Markup);
    }

    private static AngleSharp.Dom.IElement FindButtonByText(Bunit.IRenderedComponent<Import> cut, string text)
        => cut.FindAll("button").Single(button => button.TextContent.Trim().Contains(text, StringComparison.Ordinal));
}
