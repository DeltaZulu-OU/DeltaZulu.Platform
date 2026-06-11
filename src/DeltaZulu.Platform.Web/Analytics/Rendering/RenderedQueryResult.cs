
using DeltaZulu.Platform.Data.DuckDb;
using DeltaZulu.Platform.Domain.Analytics.Rendering;

namespace DeltaZulu.Platform.Web.Analytics.Rendering;
public sealed record RenderedQueryResult(
    QueryResult QueryResult,
    RenderDirective Directive,
    RenderChartModel Chart);