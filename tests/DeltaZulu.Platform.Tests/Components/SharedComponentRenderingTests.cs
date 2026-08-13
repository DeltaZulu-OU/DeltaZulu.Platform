using Bunit;
using DeltaZulu.Platform.Web.Components;
using Microsoft.AspNetCore.Components;

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

    [TestMethod]
    [DataRow("success", "dz-status-badge--success")]
    [DataRow("warning", "dz-status-badge--warning")]
    [DataRow("unknown", "dz-status-badge--neutral")]
    public void DzStatusBadge_MapsToneToCssClass(string tone, string expectedClass)
    {
        using var context = new BunitContext();

        var cut = context.Render<DzStatusBadge>(parameters => parameters
            .Add(p => p.Label, "Accepted")
            .Add(p => p.Tone, tone)
            .Add(p => p.Class, "extra-badge"));

        var badge = cut.Find(".dz-status-badge");

        AssertHasClass(badge, expectedClass);
        AssertHasClass(badge, "extra-badge");
        Assert.AreEqual("Accepted", cut.Find(".dz-status-badge__label").TextContent.Trim());
    }

    [TestMethod]
    public async Task DzFilterBar_RendersTitleSearchFiltersAndActions()
    {
        await using var context = MudBlazorTestContext.Create();

        var cut = context.Render<DzFilterBar>(parameters => parameters
            .Add(p => p.Title, "Library")
            .Add(p => p.Description, "12 items")
            .Add(p => p.SearchText, "kql")
            .Add(p => p.Filters, "All | Queries")
            .Add(p => p.Actions, "New"));

        AssertHasClass(cut.Find(".dz-toolbar"), "dz-toolbar");
        Assert.AreEqual("Library", cut.Find(".dz-filter-bar__title h2").TextContent.Trim());
        Assert.AreEqual("12 items", cut.Find(".dz-filter-bar__title p").TextContent.Trim());
        Assert.Contains("kql", cut.Find(".dz-filter-bar__search input").GetAttribute("value") ?? string.Empty);
        Assert.Contains("All | Queries", cut.Find(".dz-filter-bar__filters").TextContent);
        Assert.Contains("New", cut.Find(".dz-toolbar__actions").TextContent);
    }

    [TestMethod]
    public async Task DzDataTable_RendersRows()
    {
        await using var context = MudBlazorTestContext.Create();

        var withRows = context.Render<DzDataTable<string>>(parameters => parameters
            .Add(p => p.Items, new[] { "alpha", "bravo" })
            .Add(p => p.HeaderContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<th>Name</th>")))
            .Add(p => p.RowTemplate, (RenderFragment<string>)(item => builder => builder.AddMarkupContent(0, $"<td>{item}</td>"))));

        AssertHasClass(withRows.Find(".dz-table-shell"), "dz-table-shell");
        Assert.Contains("alpha", withRows.Markup);
        Assert.Contains("bravo", withRows.Markup);
    }

    [TestMethod]
    public async Task DzDataTable_RendersEmptyState()
    {
        await using var context = MudBlazorTestContext.Create();

        var empty = context.Render<DzDataTable<string>>(parameters => parameters
            .Add(p => p.Items, Array.Empty<string>())
            .Add(p => p.EmptyTitle, "No rows")
            .Add(p => p.HeaderContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<th>Name</th>")))
            .Add(p => p.RowTemplate, (RenderFragment<string>)(item => builder => builder.AddMarkupContent(0, $"<td>{item}</td>"))));

        Assert.AreEqual("No rows", empty.Find(".dz-empty-state__title").TextContent.Trim());
    }

    [TestMethod]
    public async Task DzDataTable_ShowsLoadingStateInsteadOfTable()
    {
        await using var context = MudBlazorTestContext.Create();

        var cut = context.Render<DzDataTable<string>>(parameters => parameters
            .Add(p => p.Loading, true)
            .Add(p => p.LoadingText, "Fetching…")
            .Add(p => p.HeaderContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<th>Name</th>")))
            .Add(p => p.RowTemplate, (RenderFragment<string>)(item => builder => builder.AddMarkupContent(0, $"<td>{item}</td>"))));

        Assert.AreEqual("Fetching…", cut.Find(".dz-loading-state__text").TextContent.Trim());
    }

    private static void AssertHasClass(AngleSharp.Dom.IElement element, string expectedClass)
    {
        var classes = element.GetAttribute("class") ?? string.Empty;

        Assert.Contains(expectedClass, classes);
    }
}