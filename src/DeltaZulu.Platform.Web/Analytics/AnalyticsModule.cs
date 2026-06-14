using DeltaZulu.Platform.Web.Components;
using DeltaZulu.Platform.Web.Platform;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;

namespace DeltaZulu.Platform.Web.Analytics;

public sealed class AnalyticsModule : IPlatformModule
{
    public PlatformModuleDescriptor Descriptor { get; } = new()
    {
        Id = "analytics",
        DisplayName = "Analytics",
        RoutePrefix = "/analytics",
        Order = 100,
    };

    public IReadOnlyList<DzNavItem> NavigationItems { get; } =
    [
        new("Analytics", "/analytics", Icons.Material.Outlined.Search, NavLinkMatch.All),
        new("Library", "/analytics/library", Icons.Material.Outlined.LibraryBooks),
        new("Dashboards", "/analytics/dashboards", Icons.Material.Outlined.Dashboard),
    ];

    public IReadOnlyList<PlatformRouteGroup> RouteGroups { get; } =
    [
        new()
        {
            RoutePrefix = "/analytics",
            PageAssembly = typeof(AnalyticsModule).Assembly,
        },
    ];

    public IReadOnlyList<PlatformStaticAssetDescriptor> StaticAssets { get; } =
    [
        new() { Href = "css/analytics-app.css", Kind = PlatformStaticAssetKind.Stylesheet, Order = 100 },
        new() { Href = "css/kql-helper-drawer.css", Kind = PlatformStaticAssetKind.Stylesheet, Order = 101 },
    ];
}