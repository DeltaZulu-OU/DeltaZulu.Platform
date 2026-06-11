namespace DeltaZulu.Platform.Web.Platform;

/// <summary>
/// Describes a static asset (CSS, JS, image) that a module requires the host to load.
/// </summary>
public sealed record PlatformStaticAssetDescriptor
{
    public required string Href { get; init; }
    public required PlatformStaticAssetKind Kind { get; init; }
    public int Order { get; init; }
}

public enum PlatformStaticAssetKind
{
    Stylesheet,
    Script,
    Image
}