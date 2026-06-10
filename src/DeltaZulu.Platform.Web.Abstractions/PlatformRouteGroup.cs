using System.Reflection;

namespace DeltaZulu.Platform.Web.Abstractions;

/// <summary>
/// Describes a group of Blazor routes contributed by a module.
/// The platform host uses these to discover page assemblies and mount routes.
/// </summary>
public sealed record PlatformRouteGroup
{
    public required string RoutePrefix { get; init; }
    public required Assembly PageAssembly { get; init; }
}
