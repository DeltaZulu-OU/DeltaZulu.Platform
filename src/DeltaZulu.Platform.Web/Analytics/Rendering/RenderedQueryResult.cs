using DeltaZulu.Platform.Domain.Analytics.Execution;
using DeltaZulu.Platform.Domain.Analytics.Rendering;

namespace DeltaZulu.Platform.Web.Analytics.Rendering;

public sealed record RenderedQueryResult(
    QueryResult QueryResult,
    RenderDirective Directive,
    RenderChartModel Chart);