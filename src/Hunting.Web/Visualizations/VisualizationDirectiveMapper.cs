namespace Hunting.Web.Visualizations;

using System.Text.Json;
using Hunting.Application.Visualizations;
using Hunting.Render.Model;

public static class VisualizationDirectiveMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool TryMap(
        VisualizationRecord visualization,
        out RenderDirective directive,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(visualization);

        directive = RenderDirective.Table();
        error = string.Empty;

        if (!Enum.TryParse<RenderKind>(visualization.Kind, ignoreCase: true, out var kind))
        {
            error = $"Visualization '{visualization.Id}' has unsupported kind '{visualization.Kind}'.";
            return false;
        }

        VisualizationSpec? spec;
        try
        {
            spec = JsonSerializer.Deserialize<VisualizationSpec>(
                visualization.SpecJson,
                JsonOptions);
        }
        catch (JsonException ex)
        {
            error = $"Visualization '{visualization.Id}' contains malformed visualization spec JSON. {ex.Message}";
            return false;
        }

        if (spec is null)
        {
            error = $"Visualization '{visualization.Id}' contains an empty visualization spec.";
            return false;
        }

        directive = new RenderDirective
        {
            Kind = kind,
            Title = NormalizeOptionalText(spec.Title),
            Legend = NormalizeOptionalText(spec.Legend),
            IsStacked = spec.IsStacked,
            Binding = new RenderBinding
            {
                XColumn = NormalizeOptionalText(spec.XColumn),
                YColumns = NormalizeColumnList(spec.YColumns),
                SeriesColumn = NormalizeOptionalText(spec.SeriesColumn)
            }
        };
        return true;
    }

    private static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<string> NormalizeColumnList(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return [];
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
