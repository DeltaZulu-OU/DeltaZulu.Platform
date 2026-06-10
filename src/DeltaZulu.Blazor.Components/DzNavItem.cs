using Microsoft.AspNetCore.Components.Routing;

namespace DeltaZulu.Blazor.Components;

/// <summary>
/// Product-neutral navigation item used by <see cref="DzSideNav" />.
/// </summary>
public sealed record DzNavItem(
    string Label,
    string Href,
    string Icon,
    NavLinkMatch Match = NavLinkMatch.Prefix,
    bool DividerBefore = false);