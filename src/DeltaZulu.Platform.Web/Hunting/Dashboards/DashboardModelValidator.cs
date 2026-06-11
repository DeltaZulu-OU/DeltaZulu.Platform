namespace DeltaZulu.Platform.Web.Hunting.Dashboards;

using DeltaZulu.Platform.Application.Hunting.Render.Directives;

public static class DashboardModelValidator
{
    private const int DashboardGridColumnCount = 12;

    private static readonly RenderDirectiveParser RenderDirectiveParser = new();

    public static IReadOnlyList<string> Validate(DashboardDefinition? dashboard)
    {
        if (dashboard is null)
        {
            return ["Dashboard is required."];
        }

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(dashboard.Id))
        {
            errors.Add("Dashboard ID is required.");
        }

        if (string.IsNullOrWhiteSpace(dashboard.Name))
        {
            errors.Add("Dashboard name is required.");
        }

        ValidateDashboardRefresh(dashboard.Refresh, errors);

        if (dashboard.UpdatedAtUtc < dashboard.CreatedAtUtc)
        {
            errors.Add("Dashboard updated timestamp cannot be earlier than created timestamp.");
        }

        ValidateWidgets(dashboard.Widgets, errors);
        return errors;
    }

    public static void ThrowIfInvalid(DashboardDefinition? dashboard)
    {
        var errors = Validate(dashboard);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }
    }

    private static void ValidateDashboardRefresh(
        DashboardRefreshPolicy? refresh,
        List<string> errors)
    {
        if (refresh is null)
        {
            errors.Add("Dashboard refresh policy is required.");
            return;
        }

        if (!refresh.Enabled && refresh.IntervalSeconds.HasValue)
        {
            errors.Add("Dashboard manual refresh policy must not define an interval.");
        }

        if (refresh.Enabled && (!refresh.IntervalSeconds.HasValue || refresh.IntervalSeconds.Value <= 0))
        {
            errors.Add("Dashboard enabled refresh policy must define a positive interval.");
        }
    }

    private static void ValidateWidgets(
        IReadOnlyList<DashboardWidgetDefinition>? widgets,
        List<string> errors)
    {
        if (widgets is null)
        {
            errors.Add("Dashboard widgets collection is required.");
            return;
        }

        var widgetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < widgets.Count; i++)
        {
            var widget = widgets[i];
            if (string.IsNullOrWhiteSpace(widget.Id))
            {
                errors.Add($"Widget at index {i} must have an ID.");
            }
            else if (!widgetIds.Add(widget.Id))
            {
                errors.Add($"Duplicate widget ID '{widget.Id}' is not allowed.");
            }

            if (string.IsNullOrWhiteSpace(widget.Title))
            {
                errors.Add($"Widget '{DisplayWidgetId(widget, i)}' must have a title.");
            }

            ValidateWidgetExecutionSource(widget, i, errors);
            ValidateLayout(widget, i, errors);
            ValidateRefresh(widget, i, errors);
        }

        ValidateLayoutConflicts(widgets, errors);
    }

    private static void ValidateWidgetExecutionSource(
        DashboardWidgetDefinition widget,
        int index,
        List<string> errors)
    {
        if (widget.Kind != DashboardWidgetKind.Query)
        {
            return;
        }

        var hasQueryText = !string.IsNullOrWhiteSpace(widget.QueryText);
        var hasVisualizationId = !string.IsNullOrWhiteSpace(widget.VisualizationId);

        if (!hasQueryText && !hasVisualizationId)
        {
            errors.Add($"Query widget '{DisplayWidgetId(widget, index)}' must have query text or a visualization ID.");
        }

        if (hasQueryText && hasVisualizationId)
        {
            errors.Add($"Query widget '{DisplayWidgetId(widget, index)}' must not define both query text and a visualization ID.");
        }

        if (hasQueryText)
        {
            ValidateWidgetRenderIntent(widget, index, errors);
        }
    }

    private static void ValidateWidgetRenderIntent(
        DashboardWidgetDefinition widget,
        int index,
        List<string> errors)
    {
        var parsed = RenderDirectiveParser.Parse(widget.QueryText);
        if (!parsed.HasRenderDirective)
        {
            errors.Add(
                $"Query widget '{DisplayWidgetId(widget, index)}' must include a render command. Use '| render' or '| render table' for a table widget, or choose a chart render type.");
        }
    }

    private static void ValidateLayout(
        DashboardWidgetDefinition widget,
        int index,
        List<string> errors)
    {
        var layout = widget.Layout;
        var widgetId = DisplayWidgetId(widget, index);

        if (layout.X < 0)
        {
            errors.Add($"Widget '{widgetId}' layout X must be zero or greater.");
        }

        if (layout.Y < 0)
        {
            errors.Add($"Widget '{widgetId}' layout Y must be zero or greater.");
        }

        if (layout.Width <= 0)
        {
            errors.Add($"Widget '{widgetId}' layout width must be greater than zero.");
        }

        if (layout.Height <= 0)
        {
            errors.Add($"Widget '{widgetId}' layout height must be greater than zero.");
        }

        if (layout.MinimumWidth <= 0)
        {
            errors.Add($"Widget '{widgetId}' layout minimum width must be greater than zero.");
        }

        if (layout.MinimumHeight <= 0)
        {
            errors.Add($"Widget '{widgetId}' layout minimum height must be greater than zero.");
        }

        if (layout.Width > 0 && layout.MinimumWidth > 0 && layout.Width < layout.MinimumWidth)
        {
            errors.Add($"Widget '{widgetId}' layout width cannot be smaller than minimum width.");
        }

        if (layout.Height > 0 && layout.MinimumHeight > 0 && layout.Height < layout.MinimumHeight)
        {
            errors.Add($"Widget '{widgetId}' layout height cannot be smaller than minimum height.");
        }

        if (layout.Width > DashboardGridColumnCount)
        {
            errors.Add($"Widget '{widgetId}' layout width cannot exceed {DashboardGridColumnCount} grid columns.");
        }

        if (layout.MinimumWidth > DashboardGridColumnCount)
        {
            errors.Add($"Widget '{widgetId}' layout minimum width cannot exceed {DashboardGridColumnCount} grid columns.");
        }

        if (layout.X >= 0 && layout.Width > 0 && layout.X + layout.Width > DashboardGridColumnCount)
        {
            errors.Add($"Widget '{widgetId}' layout X plus width cannot exceed {DashboardGridColumnCount} grid columns.");
        }
    }

    private static void ValidateLayoutConflicts(
        IReadOnlyList<DashboardWidgetDefinition> widgets,
        List<string> errors)
    {
        for (var i = 0; i < widgets.Count; i++)
        {
            var first = widgets[i];
            if (!HasValidCollisionLayout(first.Layout))
            {
                continue;
            }

            for (var j = i + 1; j < widgets.Count; j++)
            {
                var second = widgets[j];
                if (!HasValidCollisionLayout(second.Layout))
                {
                    continue;
                }

                if (LayoutsOverlap(first.Layout, second.Layout))
                {
                    errors.Add(
                        $"Widget '{DisplayWidgetId(first, i)}' layout overlaps widget '{DisplayWidgetId(second, j)}'.");
                }
            }
        }
    }

    private static bool HasValidCollisionLayout(DashboardLayout layout)
        => layout.X >= 0
            && layout.Y >= 0
            && layout.Width > 0
            && layout.Height > 0
            && layout.X + layout.Width <= DashboardGridColumnCount;

    private static bool LayoutsOverlap(DashboardLayout first, DashboardLayout second)
        => first.X < second.X + second.Width
            && first.X + first.Width > second.X
            && first.Y < second.Y + second.Height
            && first.Y + first.Height > second.Y;

    private static void ValidateRefresh(
        DashboardWidgetDefinition widget,
        int index,
        List<string> errors)
    {
        var refresh = widget.Refresh;
        var widgetId = DisplayWidgetId(widget, index);

        if (!refresh.Enabled && refresh.IntervalSeconds.HasValue)
        {
            errors.Add($"Widget '{widgetId}' manual refresh policy must not define an interval.");
        }

        if (refresh.Enabled && (!refresh.IntervalSeconds.HasValue || refresh.IntervalSeconds.Value <= 0))
        {
            errors.Add($"Widget '{widgetId}' enabled refresh policy must define a positive interval.");
        }
    }

    private static string DisplayWidgetId(DashboardWidgetDefinition widget, int index)
        => string.IsNullOrWhiteSpace(widget.Id) ? $"#{index}" : widget.Id;
}