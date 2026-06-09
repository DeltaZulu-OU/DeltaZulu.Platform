namespace DeltaZulu.Hunting.Web.Rendering;

using DeltaZulu.Hunting.Data;
using DeltaZulu.Hunting.Render.Model;

public sealed record RenderedQueryResult(
    QueryResult QueryResult,
    RenderDirective Directive,
    RenderChartModel Chart);
