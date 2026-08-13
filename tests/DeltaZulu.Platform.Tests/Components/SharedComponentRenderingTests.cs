using Bunit;
using DeltaZulu.Platform.Web.Components;

namespace DeltaZulu.Platform.Tests.Components;

[TestClass]
public sealed class SharedComponentRenderingTests
{
    [TestMethod]
    public void DzPageHeader_RendersTitleDescriptionMetadataAndActions()
    {
        using var context = new BunitContext();

        var cut = context.Render<DzPageHeader>(parameters => parameters
            .Add(p => p.Title, "Threat Hunting")
            .Add(p => p.Description, "Explore detections and dashboards.")
            .Add(p => p.Eyebrow, "Module")
            .Add(p => p.Class, "custom-header")
            .Add(p => p.Metadata, "2 routes")
            .Add(p => p.Actions, "Create"));

        var header = cut.Find(".dz-page-header");

        AssertHasClass(header, "custom-header"); Assert.AreEqual("Module", cut.Find(".dz-page-header__eyebrow").TextContent);
        Assert.AreEqual("Threat Hunting", cut.Find(".dz-page-header__title").TextContent.Trim());
        Assert.AreEqual("Explore detections and dashboards.", cut.Find(".dz-page-header__description").TextContent.Trim());
        Assert.AreEqual("2 routes", cut.Find(".dz-page-header__metadata").TextContent);
        Assert.AreEqual("Create", cut.Find(".dz-page-header__actions").TextContent);
    }

    [TestMethod]
    public void DzPanel_AppliesCompactAndCustomClasses()
    {
        using var context = new BunitContext();

        var cut = context.Render<DzPanel>(parameters => parameters
            .Add(p => p.Title, "Panel title")
            .Add(p => p.Eyebrow, "Summary")
            .Add(p => p.Compact, true)
            .Add(p => p.Class, "module-panel")
            .AddChildContent("Panel body"));

        var panel = cut.Find(".dz-panel");

        AssertHasClass(panel, "dz-panel--compact");
        AssertHasClass(panel, "module-panel");
        Assert.AreEqual("Summary", cut.Find(".dz-panel__eyebrow").TextContent);
        Assert.AreEqual("Panel title", cut.Find(".dz-panel__title").TextContent.Trim());
        Assert.AreEqual("Panel body", cut.Find(".dz-panel__body").TextContent);
    }

    [TestMethod]
    public void DzEmptyState_RendersAccessibleRegionAttributesAndActionSlot()
    {
        using var context = new BunitContext();

        var cut = context.Render<DzEmptyState>(parameters => parameters
            .Add(p => p.Title, "No detections")
            .Add(p => p.Description, "Import content to get started.")
            .Add(p => p.Class, "library-empty")
            .AddUnmatched("role", "status")
            .AddUnmatched("aria-live", "polite")
            .Add(p => p.Actions, "Import"));

        var state = cut.Find(".dz-empty-state");

        AssertHasClass(state, "library-empty");
        Assert.AreEqual("status", state.GetAttribute("role"));
        Assert.AreEqual("polite", state.GetAttribute("aria-live"));
        Assert.AreEqual("No detections", cut.Find(".dz-empty-state__title").TextContent.Trim());
        Assert.AreEqual("Import content to get started.", cut.Find(".dz-empty-state__description").TextContent.Trim());
        Assert.AreEqual("Import", cut.Find(".dz-empty-state__actions").TextContent);
    }

    [TestMethod]
    [DataRow("success", "dz-status-chip--success")]
    [DataRow("warning", "dz-status-chip--warning")]
    [DataRow("unknown", "dz-status-chip--neutral")]
    public async Task DzStatusChip_MapsToneToCssClass(string tone, string expectedClass)
    {
        await using var context = MudBlazorTestContext.Create();

        var cut = context.Render<DzStatusChip>(parameters => parameters
            .Add(p => p.Label, "Ready")
            .Add(p => p.Tone, tone)
            .Add(p => p.Class, "extra-chip"));

        var chip = cut.Find(".dz-status-chip");

        AssertHasClass(chip, expectedClass);
        AssertHasClass(chip, "extra-chip");
        Assert.AreEqual("Ready", chip.TextContent.Trim());
    }

    private static void AssertHasClass(AngleSharp.Dom.IElement element, string expectedClass)
    {
        var classes = element.GetAttribute("class") ?? string.Empty;

        Assert.Contains(expectedClass, classes);
    }
}