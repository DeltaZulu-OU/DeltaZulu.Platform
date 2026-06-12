using DeltaZulu.Platform.Web.Components;
using DeltaZulu.Platform.Web.Platform;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;

namespace DeltaZulu.Platform.Web.Governance;

public sealed class GovernanceModule : IPlatformModule
{
    public PlatformModuleDescriptor Descriptor { get; } = new()
    {
        Id = "governance",
        DisplayName = "Detection Content Governance",
        Badge = "POC",
        RoutePrefix = "/governance",
        Order = 200,
    };

    public IReadOnlyList<DzNavItem> NavigationItems { get; } =
    [
        new("Home", "/governance", Icons.Material.Outlined.Dashboard, NavLinkMatch.All),
        new("Detections", "/governance/detections", Icons.Material.Outlined.Radar),
        new("Changes", "/governance/changes", Icons.Material.Outlined.Assignment),
        new("History", "/governance/history", Icons.Material.Outlined.History),
        new("Settings", "/settings", Icons.Material.Outlined.Settings, DividerBefore: true, IsPlatformRoute: true),
    ];

    public IReadOnlyList<PlatformRouteGroup> RouteGroups { get; } =
    [
        new()
        {
            RoutePrefix = "/governance",
            PageAssembly = typeof(GovernanceModule).Assembly,
        },
    ];

    public IReadOnlyList<PlatformStaticAssetDescriptor> StaticAssets { get; } =
    [
        new() { Href = "app.css", Kind = PlatformStaticAssetKind.Stylesheet, Order = 100 },
    ];
}