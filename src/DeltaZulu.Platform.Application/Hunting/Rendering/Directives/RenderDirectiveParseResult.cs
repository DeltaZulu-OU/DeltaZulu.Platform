namespace DeltaZulu.Platform.Application.Hunting.Rendering.Directives;

using DeltaZulu.Platform.Domain.Hunting.Rendering;

public sealed record RenderDirectiveParseResult
{
    public required string QueryTextWithoutRender { get; init; }

    public required RenderDirective Directive { get; init; }

    public bool HasRenderDirective { get; init; }
}