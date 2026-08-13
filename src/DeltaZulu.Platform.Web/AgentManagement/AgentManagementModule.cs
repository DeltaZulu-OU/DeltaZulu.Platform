using DeltaZulu.Platform.Web.Components;
using DeltaZulu.Platform.Web.Platform;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;

namespace DeltaZulu.Platform.Web.AgentManagement;

public sealed class AgentManagementModule : IPlatformModule
{
    public PlatformModuleDescriptor Descriptor { get; } = new() {
        Id = "agent-management",
        DisplayName = "Agent Management",
        Badge = "POC",
        RoutePrefix = "/agents",
        Order = 300,
    };

    public IReadOnlyList<DzNavItem> NavigationItems { get; } =
    [
        new("Agents", "/agents", Icons.Material.Outlined.Memory, NavLinkMatch.All, DividerBefore: true),
        new("Fleet Health", "/agents/fleet", Icons.Material.Outlined.MonitorHeart),
        new("Telemetry Utilization", "/agents/utilization", Icons.Material.Outlined.QueryStats),
        new("Groups", "/agents/groups", Icons.Material.Outlined.Workspaces),
        new("Resource Profiles", "/agents/profiles", Icons.Material.Outlined.Description),
        new("Daemon Configs", "/agents/configs", Icons.Material.Outlined.Settings),
        new("Assignments", "/agents/assignments", Icons.Material.Outlined.Rule),
        new("Enrollment Tokens", "/agents/enrollment-tokens", Icons.Material.Outlined.Key),
    ];

    public IReadOnlyList<PlatformRouteGroup> RouteGroups { get; } =
    [
        new()
        {
            RoutePrefix = "/agents",
            PageAssembly = typeof(AgentManagementModule).Assembly,
        },
    ];

    public IReadOnlyList<PlatformStaticAssetDescriptor> StaticAssets { get; } =
    [
        new() { Href = "css/agent-management-app.css", Kind = PlatformStaticAssetKind.Stylesheet, Order = 100 },
    ];
}
