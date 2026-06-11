
using System.Globalization;
using DeltaZulu.Platform.Application.Hunting.Rendering.Tabular;
using DeltaZulu.Platform.Domain.Hunting.Rendering;

namespace DeltaZulu.Platform.Application.Hunting.Rendering.Services;
public sealed class RenderChartBuilder : IRenderChartBuilder
{
    private const int MaxChartPoints = 500;
    private readonly IRenderResolver _resolver;

    public RenderChartBuilder(IRenderResolver? resolver = null)
    {
        _resolver = resolver ?? new RenderResolver();
    }

    public RenderChartModel Build(IRenderTabularResult result, RenderDirective directive)
    {
        if (result is null)
        {
            return BuildFallback("No render data.");
        }

        ArgumentNullException.ThrowIfNull(directive);

        if (!result.Success)
        {
            return BuildFallback("No render data.");
        }

        var plan = _resolver.Resolve(directive, result.Columns);
        if (plan.IsFallback
            || plan.Kind == RenderKind.Table
            || string.IsNullOrWhiteSpace(plan.XColumn)
            || plan.YColumns.Count == 0)
        {
            return BuildFallback(plan.FallbackReason ?? "Render fell back to table.");
        }

        var xIndex = GetColumnIndex(result.Columns, plan.XColumn);
        if (xIndex < 0)
        {
            return BuildFallback("X column was not found in results.");
        }

        var yIndexes = plan.YColumns
            .Select(y => (Name: y, Index: GetColumnIndex(result.Columns, y)))
            .Where(y => y.Index >= 0)
            .ToList();
        if (yIndexes.Count == 0)
        {
            return BuildFallback("No numeric Y columns could be rendered.");
        }

        var seriesIndex = string.IsNullOrWhiteSpace(plan.SeriesColumn)
            ? -1
            : GetColumnIndex(result.Columns, plan.SeriesColumn);

        return seriesIndex >= 0
            ? BuildGroupedModel(result, plan, xIndex, seriesIndex, yIndexes)
            : BuildPlainModel(result, plan, xIndex, yIndexes);
    }

    private static RenderChartModel BuildFallback(string message)
        => new(false, message, string.Empty, string.Empty, null, [], [], 0, 1, null, false, RenderKind.Table);

    private static RenderChartModel BuildPlainModel(
        IRenderTabularResult result,
        ResolvedRenderPlan plan,
        int xIndex,
        List<(string Name, int Index)> yIndexes)
    {
        var labels = new List<string>(result.RowCount);
        var seriesValues = yIndexes.ToDictionary(
            y => y.Name,
            _ => new List<double>(result.RowCount),
            StringComparer.OrdinalIgnoreCase);

        for (var row = 0; row < result.RowCount; row++)
        {
            labels.Add(FormatXLabel(result.GetValue(row, xIndex)));
            foreach (var y in yIndexes)
            {
                seriesValues[y.Name].Add(ToDouble(result.GetValue(row, y.Index)));
            }
        }

        var series = yIndexes
            .ConvertAll(y => new RenderSeries(y.Name, seriesValues[y.Name]))
;

        return BuildFinalModel(plan, labels, series);
    }

    private static RenderChartModel BuildGroupedModel(
        IRenderTabularResult result,
        ResolvedRenderPlan plan,
        int xIndex,
        int seriesIndex,
        List<(string Name, int Index)> yIndexes)
    {
        var labels = new List<string>();
        var labelMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var grouped = new Dictionary<string, Dictionary<string, List<double>>>(StringComparer.OrdinalIgnoreCase);

        for (var row = 0; row < result.RowCount; row++)
        {
            var xLabel = FormatXLabel(result.GetValue(row, xIndex));
            if (!labelMap.TryGetValue(xLabel, out var labelIndex))
            {
                labelIndex = labels.Count;
                labelMap[xLabel] = labelIndex;
                labels.Add(xLabel);
            }

            var rawSeriesKey = result.GetValue(row, seriesIndex)?.ToString();
            var seriesKey = string.IsNullOrWhiteSpace(rawSeriesKey) ? "(null)" : rawSeriesKey!;
            if (!grouped.TryGetValue(seriesKey, out var perY))
            {
                perY = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
                grouped[seriesKey] = perY;
            }

            foreach (var y in yIndexes)
            {
                if (!perY.TryGetValue(y.Name, out var values))
                {
                    values = Enumerable.Repeat(0d, labels.Count).ToList();
                    perY[y.Name] = values;
                }
                else if (values.Count < labels.Count)
                {
                    values.AddRange(Enumerable.Repeat(0d, labels.Count - values.Count));
                }

                values[labelIndex] += ToDouble(result.GetValue(row, y.Index));
            }
        }

        var series = new List<RenderSeries>();
        foreach (var (groupName, perY) in grouped)
        {
            foreach (var y in yIndexes)
            {
                if (!perY.TryGetValue(y.Name, out var values))
                {
                    continue;
                }

                Pad(values, labels.Count);
                var name = yIndexes.Count == 1 ? groupName : $"{groupName} · {y.Name}";
                series.Add(new RenderSeries(name, values));
            }
        }

        return BuildFinalModel(plan, labels, series);
    }

    private static RenderChartModel BuildFinalModel(
        ResolvedRenderPlan plan,
        List<string> labels,
        List<RenderSeries> series)
    {
        var warning = string.Empty;
        if (labels.Count > MaxChartPoints)
        {
            (labels, series, var originalCount) = Downsample(labels, series, MaxChartPoints);
            warning = $"Render degraded due to size: sampled to {MaxChartPoints} points from {originalCount}.";
        }

        var allValues = series.SelectMany(s => s.Values).ToArray();
        var min = allValues.Length == 0 ? 0 : allValues.Min();
        var max = allValues.Length == 0 ? 1 : allValues.Max();
        if (Math.Abs(max - min) < double.Epsilon)
        {
            max = min + 1;
        }

        return new RenderChartModel(
            true,
            string.Empty,
            warning,
            plan.XColumn!,
            plan.SeriesColumn,
            labels,
            series,
            min,
            max,
            plan.Legend,
            plan.IsStacked,
            plan.Kind);
    }

    private static (List<string> Labels, List<RenderSeries> Series, int OriginalCount) Downsample(
        List<string> labels,
        List<RenderSeries> series,
        int maxPoints)
    {
        if (labels.Count <= maxPoints)
        {
            return (labels, series, labels.Count);
        }

        var indexes = new List<int>(maxPoints);
        var step = (labels.Count - 1d) / (maxPoints - 1d);
        for (var i = 0; i < maxPoints; i++)
        {
            indexes.Add((int)Math.Round(i * step));
        }

        var sampledLabels = indexes
            .ConvertAll(i => labels[Math.Min(i, labels.Count - 1)])
;
        var sampledSeries = series
            .ConvertAll(s => new RenderSeries(
                s.Name,
                indexes.ConvertAll(i => s.Values[Math.Min(i, s.Values.Count - 1)])))
;

        return (sampledLabels, sampledSeries, labels.Count);
    }

    private static int GetColumnIndex(IReadOnlyList<RenderColumn> columns, string name)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            if (string.Equals(columns[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string FormatXLabel(object? value)
        => value switch
        {
            null => "(null)",
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };

    private static double ToDouble(object? value)
    {
        if (value is null)
        {
            return 0;
        }

        return value switch
        {
            byte b => b,
            sbyte sb => sb,
            short s => s,
            ushort us => us,
            int i => i,
            uint ui => ui,
            long l => l,
            ulong ul => ul,
            float f => f,
            double d => d,
            decimal m => (double)m,
            _ => double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0
        };
    }

    private static void Pad(List<double> values, int count)
    {
        if (values.Count < count)
        {
            values.AddRange(Enumerable.Repeat(0d, count - values.Count));
        }
    }
}