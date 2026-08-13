using DeltaZulu.Platform.Domain.Analytics.Rendering;
using Vizor.ECharts;

namespace DeltaZulu.Platform.Web.Analytics.Rendering;

public sealed class EChartsRenderOptionsBuilder
{
    public ChartOptions Build(RenderChartModel chart)
    {
        ArgumentNullException.ThrowIfNull(chart);

        if (!chart.CanRender)
        {
            return new ChartOptions();
        }

        var series = new List<ISeries>();
        TooltipTrigger trigger;

        switch (chart.Kind)
        {
            case RenderKind.Barchart:
            case RenderKind.Columnchart:
                trigger = TooltipTrigger.Axis;
                series.AddRange(chart.Series.Select(s => new BarSeries {
                    Name = s.Name,
                    Stack = chart.IsStacked ? "total" : null,
                    Data = s.Values
                }));
                break;

            case RenderKind.Piechart:
                trigger = TooltipTrigger.Item;
                series.AddRange(chart.Series.Select(s => new PieSeries {
                    Name = s.Name,
                    Radius = new CircleRadius("58%"),
                    Data = BuildPieData(chart, s)
                }));
                break;

            case RenderKind.Timechart:
            case RenderKind.Linechart:
                trigger = TooltipTrigger.Axis;
                series.AddRange(chart.Series.Select(s => new LineSeries {
                    Name = s.Name,
                    Smooth = true,
                    Data = s.Values
                }));
                break;

            case RenderKind.Areachart:
                trigger = TooltipTrigger.Axis;
                series.AddRange(chart.Series.Select(s => new LineSeries {
                    Name = s.Name,
                    Smooth = true,
                    Stack = chart.IsStacked ? "total" : null,
                    AreaStyle = new AreaStyle { Opacity = 1 },
                    Data = s.Values
                }));
                break;

            case RenderKind.Scatterchart:
                trigger = TooltipTrigger.Item;
                series.AddRange(chart.Series.Select(s => new ScatterSeries {
                    Name = s.Name,
                    Data = s.Values
                }));
                break;

            default:
                throw new NotSupportedException($"Render kind '{chart.Kind}' is not yet supported in the chart adapter.");
        }

        var options = new ChartOptions {
            Tooltip = new Tooltip { Trigger = trigger, Show = true },
            Legend = new Legend { Show = ShouldShowEChartsLegend(chart) },
            Series = series
        };

        if (chart.Kind == RenderKind.Piechart)
        {
            options.XAxis = new XAxis { Show = false };
            options.YAxis = new YAxis { Show = false };
            return options;
        }

        options.XAxis = new XAxis {
            Type = AxisType.Category,
            Data = chart.XLabels.Select(v => new AxisData { Value = v }).ToList()
        };
        options.YAxis = new YAxis { Type = AxisType.Value };

        return options;
    }

    public static bool ShouldShowLegend(RenderChartModel chart)
    {
        ArgumentNullException.ThrowIfNull(chart);

        return ParseLegendVisibility(chart.Legend) == LegendVisibility.Visible;
    }

    private static bool ShouldShowEChartsLegend(RenderChartModel chart)
        => chart.Kind != RenderKind.Piechart
           && ParseLegendVisibility(chart.Legend) == LegendVisibility.Visible;

    private static LegendVisibility ParseLegendVisibility(string? legend)
    {
        var normalized = legend?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return LegendVisibility.Visible;
        }

        return normalized.ToLowerInvariant() switch {
            "hidden" or "hide" or "none" or "off" => LegendVisibility.Hidden,
            _ => LegendVisibility.Visible
        };
    }

    private static List<object> BuildPieData(RenderChartModel chart, RenderSeries series)
    {
        var points = new List<object>();
        var count = Math.Min(chart.XLabels.Count, series.Values.Count);

        for (var i = 0; i < count; i++)
        {
            points.Add(new { name = chart.XLabels[i], value = series.Values[i] });
        }

        return points;
    }
}