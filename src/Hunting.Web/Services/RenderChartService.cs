using System.Linq;

namespace Hunting.Web.Services;

using Hunting.Core.Render;
using Hunting.Data;
using Hunting.Data.Render;
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
    private string? _lastErrorMessage;

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
    /// Gets the last chart adapter error message. Returns null when chart options were built successfully.
    /// </summary>
    public string? LastErrorMessage => _lastErrorMessage;

    /// <summary>
    /// Determines whether the legend should be hidden based on the chart model.
    /// </summary>
    public static bool ShowLegend(RenderChartModel chart)
    {
        var legendValue = chart.Legend?.Trim().ToLowerInvariant();
        return legendValue is "hidden" or "hide" or "none" or "off";
    }

    /// <summary>
    /// Clears the cached chart and options.
    /// </summary>
    public void ClearCache()
    {
        _cachedChart = null;
        _cachedChartOptions = null;
        _lastErrorMessage = null;
    }

    /// <summary>
    /// Rebuilds the chart cache from the given query result.
    /// </summary>
    public void RebuildCache(QueryResult? result)
    {
        _lastErrorMessage = null;
        _cachedChart = BuildChart(result);

        try
        {
            _cachedChartOptions = BuildChartOptions(_cachedChart);
        }
        catch (NotSupportedException ex)
        {
            _lastErrorMessage = ex.Message;
            _cachedChart = null;
            _cachedChartOptions = null;
        }
    }
    /// <summary>
    /// Builds EChart options from a render chart model.
    /// </summary>
    private static ChartOptions BuildChartOptions(RenderChartModel chart)
    {
        if (!chart.CanRender)
        {
            return new ChartOptions();
        }

        var showLegend = ShowLegend(chart);
        var series = new List<ISeries>();
        TooltipTrigger trigger;

        switch (chart.Kind)
        {
            case RenderKind.Barchart:
            case RenderKind.Columnchart:
                trigger = TooltipTrigger.Axis;
                series.AddRange(chart.Series.Select(s => new BarSeries { Name = s.Name, Stack = chart.IsStacked ? "total" : null, Data = s.Values }));
                break;

            case RenderKind.Piechart:
                trigger = TooltipTrigger.Item; // CHANGE: Use Item for Pie
                series.AddRange(chart.Series.Select(s => {
                    var pieData = new List<object>();
                    for (var i = 0; i < chart.XLabels.Count && i < s.Values.Count; i++)
                    {
                        pieData.Add(new { name = chart.XLabels[i], value = s.Values[i] });
                    }

                    return new PieSeries { Name = s.Name, Radius = new CircleRadius("65%"), Data = pieData };
                }));
                break;

            case RenderKind.Timechart:
            case RenderKind.Linechart:
                trigger = TooltipTrigger.Axis;
                series.AddRange(chart.Series.Select(s => new LineSeries { Name = s.Name, Smooth = true, Data = s.Values }));
                break;

            case RenderKind.Areachart:
                trigger = TooltipTrigger.Axis;
                series.AddRange(chart.Series.Select(s => new LineSeries { Name = s.Name, Smooth = true, Stack = chart.IsStacked ? "total" : null, AreaStyle = new AreaStyle { Opacity = 1 }, Data = s.Values }));
                break;

            case RenderKind.Scatterchart:
                trigger = TooltipTrigger.Item; // CHANGE: Use Item for Scatter
                series.AddRange(chart.Series.Select(s => new ScatterSeries { Name = s.Name, Data = s.Values }));
                break;

            default:
                throw new NotSupportedException($"Render kind '{chart.Kind}' is not yet supported in the chart adapter.");
        }

        var opt = new ChartOptions
        {
            Tooltip = new Tooltip { Trigger = trigger, Show = true }, // Always enable tooltip display — Show = true for clarity
            Legend = new Legend { Show = showLegend },
            Series = series
        };

        if (chart.Kind != RenderKind.Piechart) // Pie charts don't use XAxis
        {
            opt.XAxis = new XAxis
            {
                Type = AxisType.Category,
                Data = chart.XLabels.Select(v => new AxisData { Value = v }).ToList()
            };
            opt.YAxis = new YAxis { Type = AxisType.Value };
        }
        else
        {
            opt.XAxis = new XAxis() { Show = false };
            opt.YAxis = new YAxis() { Show = false };
        }

        return opt;
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
}