namespace DeltaZulu.Platform.Application.Hunting.Rendering.Directives;

public interface IRenderDirectiveParser
{
    RenderDirectiveParseResult Parse(string queryText);
}