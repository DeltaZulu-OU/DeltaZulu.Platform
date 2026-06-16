using DeltaZulu.Platform.Domain.Analytics.Rendering;

namespace DeltaZulu.Platform.Application.Analytics.Rendering.Directives;

public sealed record RenderDirectiveParseResult
{
    public required string QueryTextWithoutRender { get; init; }

    public required RenderDirective Directive { get; init; }

    public bool HasRenderDirective { get; init; }
}