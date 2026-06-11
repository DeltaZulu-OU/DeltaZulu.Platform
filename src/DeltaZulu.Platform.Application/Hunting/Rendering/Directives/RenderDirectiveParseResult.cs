
using DeltaZulu.Platform.Domain.Hunting.Rendering;

namespace DeltaZulu.Platform.Application.Hunting.Rendering.Directives;
public sealed record RenderDirectiveParseResult
{
    public required string QueryTextWithoutRender { get; init; }

    public required RenderDirective Directive { get; init; }

    public bool HasRenderDirective { get; init; }
}