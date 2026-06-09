namespace DeltaZulu.Hunting.Render.Directives;

using DeltaZulu.Hunting.Render.Model;

public sealed record RenderDirectiveParseResult
{
    public required string QueryTextWithoutRender { get; init; }

    public required RenderDirective Directive { get; init; }

    public bool HasRenderDirective { get; init; }
}
