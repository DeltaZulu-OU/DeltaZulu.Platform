using DeltaZulu.Platform.Web.Components;
using DeltaZulu.Platform.Web.Platform;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;

namespace DeltaZulu.Platform.Web.Operations;

/// <summary>
/// Provides the Operations navigation and route boundary while the operational
/// data pipeline is delivered in the subsequent roadmap phases.
/// </summary>
public sealed class OperationsModule : IPlatformModule
{
    public PlatformModuleDescriptor Descriptor { get; } = new() {
        Id = "operations",
        DisplayName = "Operations",
        Badge = "Preview",
        RoutePrefix = "/operations",
        Order = 250,
    };

    public IReadOnlyList<DzNavItem> NavigationItems { get; } =
    [
        new("Operations", "/operations", Icons.Material.Outlined.WorkOutline, NavLinkMatch.All, DividerBefore: true),
        new("Detection Runs", "/operations/runs", Icons.Material.Outlined.PlayCircleOutline),
        new("Alert Queue", "/operations/alerts", Icons.Material.Outlined.NotificationsNone),
        new("Incident Candidates", "/operations/candidates", Icons.Material.Outlined.AccountTree),
        new("Operations Health", "/operations/health", Icons.Material.Outlined.MonitorHeart),
    ];

    public IReadOnlyList<PlatformRouteGroup> RouteGroups { get; } =
    [
        new()
        {
            RoutePrefix = "/operations",
            PageAssembly = typeof(OperationsModule).Assembly,
        },
    ];

    public IReadOnlyList<PlatformStaticAssetDescriptor> StaticAssets { get; } = [];
}
