using Microsoft.AspNetCore.Components.Routing;
using DeltaZulu.Blazor.Components;
using MudBlazor;

namespace Workbench.Web.Components.Layout;

/// <summary>
/// Workbench-owned shell metadata for the current standalone host.
/// Future DeltaZulu.Platform.Web should own equivalent chrome and compose Workbench routes as a module.
/// </summary>
public static class WorkbenchShell
{
    /// <summary>Standalone Workbench product title.</summary>
    public const string ProductName = "Detection Content Workbench";

    /// <summary>Standalone Workbench proof-of-concept badge.</summary>
    public const string ProductBadge = "POC";

    /// <summary>Standalone host logo path. A central host should replace this with platform branding.</summary>
    public const string LogoSrc = "logo-light.png";

    /// <summary>Workbench module navigation items exposed to the current host shell.</summary>
    public static readonly IReadOnlyList<DzNavItem> ModuleNavItems =
    [
        new("Home", "/", Icons.Material.Outlined.Dashboard, NavLinkMatch.All),
        new("Detections", "/detections", Icons.Material.Outlined.Radar),
        new("Changes", "/changes", Icons.Material.Outlined.Assignment),
        new("History", "/history", Icons.Material.Outlined.History),
        new("Settings", "/settings", Icons.Material.Outlined.Settings, DividerBefore: true),
    ];
}
