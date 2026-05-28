namespace Hunting.Web.Services;

using Hunting.Core.Render;
using Hunting.Data.Render;
using Hunting.Data;
using Vizor.ECharts;

/// <summary>
/// Service for building and caching ECharts visualization options.
/// Encapsulates all logic related to rendering query results as charts.
/// </summary>
public sealed class RenderChartService
{
    private readonly RenderChartBuilder _renderChartBuilder;
    private RenderChartModel? _cachedChart;
    private ChartOptions? _cachedChartOptions;

    public RenderChartService(RenderChartBuilder renderChartBuilder)
    {
        _renderChartBuilder = renderChartBuilder ?? throw new ArgumentNullException(nameof(renderChartBuilder));
    }

    /// <summary>
    /// Gets the cached chart model. Returns null if no chart has been built.
    /// </summary>
    public RenderChartModel? CachedChart => _cachedChart;

    /// <summary>
    /// Gets the cached chart options. Returns null if no chart options have been built.
    /// </summary>
    public ChartOptions? CachedChartOptions => _cachedChartOptions;

    /// <summary>
    /// Rebuilds the chart cache from the given query result.
    /// </summary>
    public void RebuildCache(QueryResult? result)
    {
        _cachedChart = BuildChart(result);
        _cachedChartOptions = BuildChartOptions(_cachedChart);
    }

    /// <summary>
    /// Clears the cached chart and options.
    /// </summary>
    public void ClearCache()
    {
        _cachedChart = null;
        _cachedChartOptions = null;
    }

    /// <summary>
    /// Builds a render chart model from query results.
    /// </summary>
    private RenderChartModel BuildChart(QueryResult? result)
    {
        if (result is null)
        {
            return new RenderChartModel(false, "No render data.", string.Empty, string.Empty, null, [], [], 0, 1, null, false, RenderKind.Table);
        }

        return _renderChartBuilder.Build(result);
    }

    /// <summary>
    /// Builds EChart options from a render chart model.
    /// </summary>
    private static ChartOptions BuildChartOptions(RenderChartModel chart)
    {
        var showLegend = !IsLegendHidden(chart);
        var series = new List<ISeries>();

        foreach (var s in chart.Series)
        {
            switch (chart.Kind)
            {
                case RenderKind.Barchart:
                case RenderKind.Columnchart:
                    series.Add(new BarSeries { Name = s.Name, Stack = chart.IsStacked ? "total" : null, Data = s.Values });
                    break;

                case RenderKind.Piechart:
                    {
                        var pieData = new List<object>();
                        for (var i = 0; i < chart.XLabels.Count && i < s.Values.Count; i++)
                        {
                            pieData.Add(new { name = chart.XLabels[i], value = s.Values[i] });
                        }

                        series.Add(new PieSeries { Name = s.Name, Radius = new CircleRadius("65%"), Data = pieData });
                        break;
                    }
                case RenderKind.Timechart:
                case RenderKind.Linechart:
                    series.Add(new LineSeries { Name = s.Name, Smooth = true, Data = s.Values });
                    break;
                case RenderKind.Areachart:
                    series.Add(new LineSeries { Name = s.Name, Smooth = true, Stack = chart.IsStacked ? "total" : null, AreaStyle = new AreaStyle { Opacity = 1 }, Data = s.Values });
                    break;
                case RenderKind.Scatterchart:
                    series.Add(new ScatterSeries { Name = s.Name, Data = s.Values });
                    break;
                case RenderKind.Card:
                default:
                    throw new NotImplementedException($"Unsupported render chart kind '{chart.Kind}' for chart adapter.");
            }
        }

        var chartOptions = new ChartOptions
        {
            Tooltip = new Tooltip { Trigger = TooltipTrigger.Axis },
            Legend = new Legend { Show = showLegend },
            XAxis = new XAxis 
            { 
                Type = AxisType.Category
            },
            YAxis = new YAxis { Type = AxisType.Value },
            Series = series
        };

        return chartOptions;
    }

    /// <summary>
    /// Determines whether the legend should be hidden based on the chart model.
    /// </summary>
    public static bool IsLegendHidden(RenderChartModel chart)
    {
        var legendValue = chart.Legend?.Trim().ToLowerInvariant();
        return legendValue is "hidden" or "hide" or "none" or "off";
    }
}
