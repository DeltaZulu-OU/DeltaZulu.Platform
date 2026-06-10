using DeltaZulu.Blazor.Components;
using DeltaZulu.Platform.Web.Abstractions;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;

namespace DeltaZulu.Hunting.Web;

public sealed class HuntingModule : IPlatformModule
{
    public PlatformModuleDescriptor Descriptor { get; } = new()
    {
        Id = "hunting",
        DisplayName = "Hunting Workbench",
        RoutePrefix = "/hunting",
        Order = 100,
    };

    public IReadOnlyList<DzNavItem> NavigationItems { get; } =
    [
        new("Overview", "/hunting", Icons.Material.Outlined.Home, NavLinkMatch.All),
        new("Threat Hunting", "/hunting/threat-hunting", Icons.Material.Outlined.Search),
        new("Library", "/hunting/library", Icons.Material.Outlined.LibraryBooks),
        new("Dashboards", "/hunting/dashboards", Icons.Material.Outlined.Dashboard),
        new("Settings", "/hunting/settings", Icons.Material.Outlined.Settings, DividerBefore: true),
    ];

    public IReadOnlyList<PlatformRouteGroup> RouteGroups { get; } =
    [
        new()
        {
            RoutePrefix = "/hunting",
            PageAssembly = typeof(HuntingModule).Assembly,
        },
    ];

    public IReadOnlyList<PlatformStaticAssetDescriptor> StaticAssets { get; } =
    [
        new() { Href = "css/app.css", Kind = PlatformStaticAssetKind.Stylesheet, Order = 100 },
        new() { Href = "css/kql-helper-drawer.css", Kind = PlatformStaticAssetKind.Stylesheet, Order = 101 },
    ];
}