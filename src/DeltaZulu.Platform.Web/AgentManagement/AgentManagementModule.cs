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
        new("Groups", "/agents/groups", Icons.Material.Outlined.Workspaces),
        new("Resource Profiles", "/agents/profiles", Icons.Material.Outlined.Description),
        new("Daemon Configs", "/agents/configs", Icons.Material.Outlined.Settings),
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
