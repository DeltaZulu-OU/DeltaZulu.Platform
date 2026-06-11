using DeltaZulu.Platform.Web.Components;

namespace DeltaZulu.Platform.Web.Platform;

/// <summary>
/// Contract that each module implements so the platform host can compose them
/// without hard-coding knowledge of any individual module.
/// </summary>
public interface IPlatformModule
{
    PlatformModuleDescriptor Descriptor { get; }
    IReadOnlyList<DzNavItem> NavigationItems { get; }
    IReadOnlyList<PlatformRouteGroup> RouteGroups { get; }
    IReadOnlyList<PlatformStaticAssetDescriptor> StaticAssets { get; }
}