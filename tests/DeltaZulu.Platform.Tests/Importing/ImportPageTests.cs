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
}
