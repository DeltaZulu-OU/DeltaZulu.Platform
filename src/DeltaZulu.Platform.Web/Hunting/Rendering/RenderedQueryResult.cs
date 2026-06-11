namespace DeltaZulu.Platform.Web.Hunting.Rendering;

using DeltaZulu.Platform.Data.Hunting;
using DeltaZulu.Platform.Application.Hunting.Render.Model;

public sealed record RenderedQueryResult(
    QueryResult QueryResult,
    RenderDirective Directive,
    RenderChartModel Chart);