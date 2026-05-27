namespace Hunting.Core.Render;

public sealed record ResolvedRenderPlan(
    RenderKind Kind,
    string? Title,
    string? XColumn,
    IReadOnlyList<string> YColumns,
    bool IsFallback,
    string? FallbackReason);

public sealed class RenderResolver
{
    private static readonly HashSet<string> NumericTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "TINYINT", "SMALLINT", "INTEGER", "BIGINT", "HUGEINT", "UTINYINT", "USMALLINT", "UINTEGER", "UBIGINT", "FLOAT", "DOUBLE", "DECIMAL", "REAL"
    };

    private static readonly HashSet<string> DateTimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "DATE", "TIMESTAMP", "TIMESTAMP_S", "TIMESTAMP_MS", "TIMESTAMP_NS", "DATETIME"
    };

    public ResolvedRenderPlan Resolve(RenderSpec spec, IReadOnlyList<(string Name, string TypeName)> schema)
    {
        if (spec.Kind == RenderKind.Table)
        {
            return new ResolvedRenderPlan(RenderKind.Table, spec.Title, null, [], spec.IsFallback, spec.FallbackReason);
        }

        var nameMap = schema.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        var xColumn = ResolveXColumn(spec, schema, nameMap);
        if (xColumn is null)
        {
            return Fallback("Unable to resolve xcolumn for render.", spec);
        }

        var yColumns = ResolveYColumns(spec, schema, nameMap);
        if (yColumns.Count == 0)
        {
            return Fallback("Unable to resolve ycolumns for render.", spec);
        }

        return new ResolvedRenderPlan(spec.Kind, spec.Title, xColumn, yColumns, false, null);
    }

    private static ResolvedRenderPlan Fallback(string reason, RenderSpec spec)
        => new(RenderKind.Table, spec.Title, null, [], true, reason);

    private static string? ResolveXColumn(RenderSpec spec, IReadOnlyList<(string Name, string TypeName)> schema, Dictionary<string, (string Name, string TypeName)> nameMap)
    {
        if (!string.IsNullOrWhiteSpace(spec.XColumn) && nameMap.TryGetValue(spec.XColumn, out var explicitColumn))
        {
            return explicitColumn.Name;
        }

        var firstDate = schema.FirstOrDefault(c => DateTimeTypes.Contains(c.TypeName));
        if (!string.IsNullOrWhiteSpace(firstDate.Name))
        {
            return firstDate.Name;
        }

        return schema.FirstOrDefault().Name;
    }

    private static IReadOnlyList<string> ResolveYColumns(RenderSpec spec, IReadOnlyList<(string Name, string TypeName)> schema, Dictionary<string, (string Name, string TypeName)> nameMap)
    {
        if (spec.YColumns.Count > 0)
        {
            var resolved = new List<string>();
            foreach (var y in spec.YColumns)
            {
                if (!nameMap.TryGetValue(y, out var column) || !NumericTypes.Contains(column.TypeName))
                {
                    continue;
                }

                resolved.Add(column.Name);
            }

            return resolved;
        }

        var firstNumeric = schema.FirstOrDefault(c => NumericTypes.Contains(c.TypeName));
        return string.IsNullOrWhiteSpace(firstNumeric.Name) ? [] : [firstNumeric.Name];
    }
}
