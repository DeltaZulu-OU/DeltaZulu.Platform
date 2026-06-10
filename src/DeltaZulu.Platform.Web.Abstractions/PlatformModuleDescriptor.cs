namespace DeltaZulu.Platform.Web.Abstractions;

/// <summary>
/// Identity and display metadata for a platform module.
/// </summary>
public sealed record PlatformModuleDescriptor
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string? Badge { get; init; }
    public string? LogoSrc { get; init; }
    public required string RoutePrefix { get; init; }
    public int Order { get; init; }
}
