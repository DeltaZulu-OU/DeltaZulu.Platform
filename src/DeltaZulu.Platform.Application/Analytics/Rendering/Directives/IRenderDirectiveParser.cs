namespace DeltaZulu.Platform.Application.Analytics.Rendering.Directives;

public interface IRenderDirectiveParser
{
    RenderDirectiveParseResult Parse(string queryText);
}