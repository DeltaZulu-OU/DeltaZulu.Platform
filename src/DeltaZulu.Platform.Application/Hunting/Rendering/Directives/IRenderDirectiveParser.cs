namespace DeltaZulu.Platform.Application.Hunting.Render.Directives;

public interface IRenderDirectiveParser
{
    RenderDirectiveParseResult Parse(string queryText);
}