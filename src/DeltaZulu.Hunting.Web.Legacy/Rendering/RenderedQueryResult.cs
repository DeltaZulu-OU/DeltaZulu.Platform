namespace Hunting.Web.Rendering;

using Hunting.Data;
using Hunting.Render.Model;

public sealed record RenderedQueryResult(
    QueryResult QueryResult,
    RenderDirective Directive,
    RenderChartModel Chart);
