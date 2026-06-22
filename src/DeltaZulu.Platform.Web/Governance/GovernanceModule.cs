using DeltaZulu.Platform.Web.Components;
using DeltaZulu.Platform.Web.Platform;
using MudBlazor;

namespace DeltaZulu.Platform.Web.Governance;

public sealed class GovernanceModule : IPlatformModule
{
    public PlatformModuleDescriptor Descriptor { get; } = new() {
        Id = "governance",
        DisplayName = "Detection Content Governance",
        Badge = "POC",
        RoutePrefix = "/governance",
        Order = 200,
    };

    public IReadOnlyList<DzNavItem> NavigationItems { get; } =
    [
        new("Detections", "/governance/detections", Icons.Material.Outlined.Radar),
        new("Detection Drafts", "/governance/drafts", Icons.Material.Outlined.Assignment),
        new("History", "/governance/history", Icons.Material.Outlined.History),
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
        new() { Href = "css/governance-app.css", Kind = PlatformStaticAssetKind.Stylesheet, Order = 100 },
    ];
}