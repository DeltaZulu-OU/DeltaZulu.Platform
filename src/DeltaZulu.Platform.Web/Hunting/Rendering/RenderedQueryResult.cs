namespace DeltaZulu.Platform.Web.Hunting.Rendering;

using DeltaZulu.Platform.Data.DuckDb;
using DeltaZulu.Platform.Domain.Hunting.Rendering;

public sealed record RenderedQueryResult(
    QueryResult QueryResult,
    RenderDirective Directive,
    RenderChartModel Chart);