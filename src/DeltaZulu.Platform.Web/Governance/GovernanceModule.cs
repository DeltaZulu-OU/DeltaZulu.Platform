using DeltaZulu.Platform.Web.Components;
using DeltaZulu.Platform.Web.Platform;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;

namespace DeltaZulu.Platform.Web.Governance;

public sealed class GovernanceModule : IPlatformModule
{
    public PlatformModuleDescriptor Descriptor { get; } = new()
    {
        Id = "workbench",
        DisplayName = "Detection Content Governance",
        Badge = "POC",
        RoutePrefix = "/workbench",
        Order = 200,
    };

    public IReadOnlyList<DzNavItem> NavigationItems { get; } =
    [
        new("Home", "/workbench", Icons.Material.Outlined.Dashboard, NavLinkMatch.All),
        new("Detections", "/workbench/detections", Icons.Material.Outlined.Radar),
        new("Changes", "/workbench/changes", Icons.Material.Outlined.Assignment),
        new("History", "/workbench/history", Icons.Material.Outlined.History),
        new("Settings", "/settings", Icons.Material.Outlined.Settings, DividerBefore: true),
    ];

    public IReadOnlyList<PlatformRouteGroup> RouteGroups { get; } =
    [
        new()
        {
            RoutePrefix = "/workbench",
            PageAssembly = typeof(GovernanceModule).Assembly,
        },
    ];

    public IReadOnlyList<PlatformStaticAssetDescriptor> StaticAssets { get; } =
    [
        new() { Href = "app.css", Kind = PlatformStaticAssetKind.Stylesheet, Order = 100 },
    ];
}