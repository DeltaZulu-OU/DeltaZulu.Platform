using DeltaZulu.Platform.Application.Analytics.Rendering.Tabular;
using DeltaZulu.Platform.Domain.Analytics.Rendering;

namespace DeltaZulu.Platform.Application.Analytics.Rendering.Services;

public sealed class RenderResolver : IRenderResolver
{
    public ResolvedRenderPlan Resolve(
        RenderDirective directive,
        IReadOnlyList<RenderColumn> columns)
    {
        ArgumentNullException.ThrowIfNull(directive);
        ArgumentNullException.ThrowIfNull(columns);

        if (directive.Kind == RenderKind.Table)
        {
            return new ResolvedRenderPlan(
                RenderKind.Table,
                directive.Title,
                null,
                [],
                null,
                null,
                false,
                directive.IsFallback,
                directive.FallbackReason);
        }

        var nameMap = columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        var xColumn = ResolveXColumn(directive, columns, nameMap);
        if (xColumn is null)
        {
            return Fallback("Unable to resolve xcolumn for render.", directive);
        }

        var yColumns = ResolveYColumns(directive, columns, nameMap);
        if (yColumns.Count == 0)
        {
            return Fallback("Unable to resolve ycolumns for render.", directive);
        }

        var seriesColumn = ResolveSeriesColumn(directive, nameMap);
        return new ResolvedRenderPlan(
            directive.Kind,
            directive.Title,
            xColumn,
            yColumns,
            seriesColumn,
            directive.Legend,
            directive.IsStacked,
            false,
            null);
    }

    private static ResolvedRenderPlan Fallback(string reason, RenderDirective directive)
        => new(
            RenderKind.Table,
            directive.Title,
            null,
            [],
            null,
            null,
            false,
            true,
            reason);

    private static string? ResolveSeriesColumn(
        RenderDirective directive,
        IReadOnlyDictionary<string, RenderColumn> nameMap)
    {
        var seriesColumn = directive.Binding.SeriesColumn;
        if (string.IsNullOrWhiteSpace(seriesColumn))
        {
            return null;
        }

        return nameMap.TryGetValue(seriesColumn, out var resolved) ? resolved.Name : null;
    }

    private static string? ResolveXColumn(
        RenderDirective directive,
        IReadOnlyList<RenderColumn> columns,
        IReadOnlyDictionary<string, RenderColumn> nameMap)
    {
        var xColumn = directive.Binding.XColumn;
        if (!string.IsNullOrWhiteSpace(xColumn) && nameMap.TryGetValue(xColumn, out var explicitColumn))
        {
            return explicitColumn.Name;
        }

        var firstTemporal = columns.FirstOrDefault(c => c.IsTemporal);
        if (firstTemporal is not null)
        {
            return firstTemporal.Name;
        }
        return columns.Count > 0 ? columns[0].Name : null;
    }

    private static IReadOnlyList<string> ResolveYColumns(
        RenderDirective directive,
        IReadOnlyList<RenderColumn> columns,
        IReadOnlyDictionary<string, RenderColumn> nameMap)
    {
        if (directive.Binding.YColumns.Count > 0)
        {
            var resolved = new List<string>();
            foreach (var yColumn in directive.Binding.YColumns)
            {
                if (nameMap.TryGetValue(yColumn, out var column) && column.IsNumeric)
                {
                    resolved.Add(column.Name);
                }
            }

            return resolved;
        }

        var firstNumeric = columns.FirstOrDefault(c => c.IsNumeric);
        return firstNumeric is null ? [] : [firstNumeric.Name];
    }
}