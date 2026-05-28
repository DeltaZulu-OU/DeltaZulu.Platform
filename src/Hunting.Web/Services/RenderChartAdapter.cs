namespace Hunting.Web.Services;

using Hunting.Core.Render;
using Hunting.Data;

public sealed record RenderSeries(string Name, IReadOnlyList<double> Values, string Color);

public sealed record RenderChartModel(
    bool CanRender,
    string Message,
    string Warning,
    string XColumn,
    string? SeriesColumn,
    IReadOnlyList<string> XLabels,
    IReadOnlyList<RenderSeries> Series,
    double YMin,
    double YMax,
    string? Legend,
    bool IsStacked,
    RenderKind Kind);

public sealed class RenderChartAdapter
{
    private static readonly string[] Palette = ["#3B82F6", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6", "#06B6D4"];
    private const int MaxChartPoints = 500;
    private readonly RenderResolver _resolver = new();

    public RenderChartModel Build(QueryResult result)
    {
        var schema = result.Columns.Select(c => (c.Name, c.TypeName)).ToArray();
        var plan = _resolver.Resolve(result.RenderSpec, schema);
        if (plan.IsFallback || plan.Kind == RenderKind.Table || string.IsNullOrWhiteSpace(plan.XColumn) || plan.YColumns.Count == 0)
        {
            return BuildFallback(plan.FallbackReason ?? "Render fell back to table.");
        }

        var xIndex = GetColumnIndex(result, plan.XColumn);
        if (xIndex < 0)
        {
            return BuildFallback("X column was not found in results.");
        }

        var yIndexes = plan.YColumns.Select(y => (Name: y, Index: GetColumnIndex(result, y))).Where(x => x.Index >= 0).ToArray();
        if (yIndexes.Length == 0)
        {
            return BuildFallback("No numeric Y columns could be rendered.");
        }

        var seriesIndex = string.IsNullOrWhiteSpace(plan.SeriesColumn) ? -1 : GetColumnIndex(result, plan.SeriesColumn);
        return seriesIndex >= 0
            ? BuildGroupedModel(result, plan, xIndex, seriesIndex, yIndexes)
            : BuildPlainModel(result, plan, xIndex, yIndexes);
    }

    private static RenderChartModel BuildFallback(string message)
        => new(false, message, string.Empty, string.Empty, null, [], [], 0, 1, null, false, RenderKind.Table);

    private RenderChartModel BuildPlainModel(QueryResult result, ResolvedRenderPlan plan, int xIndex, IReadOnlyList<(string Name, int Index)> yIndexes)
    {
        var labels = new List<string>(result.RowCount);
        var seriesValues = yIndexes.ToDictionary(x => x.Name, _ => new List<double>(result.RowCount), StringComparer.OrdinalIgnoreCase);
        for (var row = 0; row < result.RowCount; row++)
        {
            labels.Add(FormatXLabel(result.GetValue(row, xIndex)));
            foreach (var y in yIndexes)
            {
                seriesValues[y.Name].Add(ToDouble(result.GetValue(row, y.Index)));
            }
        }

        var series = yIndexes.Select((y, i) => new RenderSeries(y.Name, seriesValues[y.Name], Palette[i % Palette.Length])).ToArray();
        return BuildFinalModel(plan, labels, series);
    }

    private RenderChartModel BuildGroupedModel(QueryResult result, ResolvedRenderPlan plan, int xIndex, int seriesIndex, IReadOnlyList<(string Name, int Index)> yIndexes)
    {
        var labels = new List<string>();
        var labelMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var grouped = new Dictionary<string, Dictionary<string, List<double>>>(StringComparer.OrdinalIgnoreCase);

        for (var row = 0; row < result.RowCount; row++)
        {
            var xLabel = FormatXLabel(result.GetValue(row, xIndex));
            if (!labelMap.TryGetValue(xLabel, out var value))
            {
                value = labels.Count;
                labelMap[xLabel] = value;
                labels.Add(xLabel);
            }

            var sKey = result.GetValue(row, seriesIndex)?.ToString();
            var seriesKey = string.IsNullOrWhiteSpace(sKey) ? "(null)" : sKey!;
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

                values[value] += ToDouble(result.GetValue(row, y.Index));
            }
        }

        var series = new List<RenderSeries>();
        foreach (var (groupName, perY) in grouped)
        {
            foreach (var y in yIndexes)
            {
                if (perY.TryGetValue(y.Name, out var values))
                {
                    var name = yIndexes.Count == 1 ? groupName : $"{groupName} · {y.Name}";
                    series.Add(new RenderSeries(name, values, Palette[series.Count % Palette.Length]));
                }
            }
        }

        return BuildFinalModel(plan, labels, series);
    }

    private static RenderChartModel BuildFinalModel(ResolvedRenderPlan plan, IReadOnlyList<string> labels, IReadOnlyList<RenderSeries> series)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(series);

        var warning = string.Empty;
        if (labels.Count > MaxChartPoints)
        {
            var (Labels, Series, OriginalCount) = Downsample(labels, series, MaxChartPoints);
            labels = Labels;
            series = Series;
            warning = $"Render degraded due to size: sampled to {MaxChartPoints} points from {OriginalCount}.";
        }

        var allValues = series.SelectMany(v => v.Values).ToArray();
        var min = allValues.Length == 0 ? 0 : allValues.Min();
        var max = allValues.Length == 0 ? 1 : allValues.Max();
        if (Math.Abs(max - min) < double.Epsilon)
        {
            max = min + 1;
        }

        return new RenderChartModel(true, string.Empty, warning, plan.XColumn!, plan.SeriesColumn, labels, series, min, max, plan.Legend, plan.IsStacked, plan.Kind);
    }

    private static (IReadOnlyList<string> Labels, IReadOnlyList<RenderSeries> Series, int OriginalCount) Downsample(
        IReadOnlyList<string> labels,
        IReadOnlyList<RenderSeries> series,
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

        var sampledLabels = indexes.Select(i => labels[Math.Min(i, labels.Count - 1)]).ToArray();
        var sampledSeries = series.Select(s =>
            new RenderSeries(
                s.Name,
                indexes.Select(i => s.Values[Math.Min(i, s.Values.Count - 1)]).ToArray(),
                s.Color)).ToArray();

        return (sampledLabels, sampledSeries, labels.Count);
    }

    private static int GetColumnIndex(QueryResult result, string name)
    {
        for (var i = 0; i < result.Columns.Count; i++)
        {
            if (string.Equals(result.Columns[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string FormatXLabel(object? value)
    {
        if (value is null)
        {
            return "(null)";
        }

        return value switch
        {
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss"),
            DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss"),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static double ToDouble(object? value)
    {
        if (value is null)
        {
            return 0;
        }

        return value switch
        {
            byte v => v,
            sbyte v => v,
            short v => v,
            ushort v => v,
            int v => v,
            uint v => v,
            long v => v,
            ulong v => v,
            float v => v,
            double v => v,
            decimal v => (double)v,
            _ => double.TryParse(value.ToString(), out var parsed) ? parsed : 0
        };
    }
}
