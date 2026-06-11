
using DeltaZulu.Platform.Web.Hunting.Dashboards.Runtime;

namespace DeltaZulu.Platform.Web.Hunting.Dashboards.PageState;
public sealed class DashboardPageState
{
    public Dictionary<string, DashboardWidgetRunResult> WidgetResults { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public DashboardDefinition? Dashboard { get; set; }

    public DashboardWidgetDefinition? EditingWidget { get; set; }

    public DashboardLayout NextWidgetLayout { get; set; } = new();

    public string? Error { get; set; }

    public string? SaveError { get; set; }

    public bool AutoRefreshEnabled { get; set; }

    public bool EditorOpen { get; set; }

    public bool EditMode { get; set; }

    public bool Loading { get; set; } = true;

    public bool SettingsEditorOpen { get; set; }

    public bool CanRunDashboard
        => Dashboard?.Widgets.Any(CanRunWidget) == true;

    public bool CanStartAutoRefresh
        => CanRunDashboard
            && Dashboard?.Refresh.Enabled == true
            && Dashboard.Refresh.IntervalSeconds is > 0;

    public string AutoRefreshStatus
        => Dashboard?.Refresh.IntervalSeconds is int seconds
            ? $"Auto-refresh is running every {seconds} seconds."
            : "Auto-refresh is running.";

    public static bool CanRunWidget(DashboardWidgetDefinition widget)
        => widget.Kind == DashboardWidgetKind.Query
            && !string.IsNullOrWhiteSpace(widget.QueryText);

    public void ResetForLoad()
    {
        WidgetResults.Clear();
        EditingWidget = null;
        NextWidgetLayout = new DashboardLayout();
        Error = null;
        SaveError = null;
        AutoRefreshEnabled = false;
        EditorOpen = false;
        EditMode = false;
        SettingsEditorOpen = false;
        Dashboard = null;
        Loading = true;
    }

    public void CloseWidgetEditor()
    {
        EditorOpen = false;
        EditingWidget = null;
        NextWidgetLayout = new DashboardLayout();
    }

    public void CloseDashboardSettingsEditor()
        => SettingsEditorOpen = false;
}