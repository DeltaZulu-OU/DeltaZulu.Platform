namespace DeltaZulu.Platform.Domain.Analytics.Rendering;

public sealed record RenderDirective
{
    public RenderKind Kind { get; init; } = RenderKind.Table;

    public string? Title { get; init; }

    public RenderBinding Binding { get; init; } = new();

    public string? Legend { get; init; }

    public bool IsStacked { get; init; }

    public bool IsFallback { get; init; }

    public string? FallbackReason { get; init; }

    public static RenderDirective Table(string? reason = null)
        => new()
        {
            Kind = RenderKind.Table,
            IsFallback = reason is not null,
            FallbackReason = reason
        };
}