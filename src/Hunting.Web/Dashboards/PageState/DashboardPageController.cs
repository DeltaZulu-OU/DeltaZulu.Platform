namespace Hunting.Web.Dashboards.PageState;

using Hunting.Web.Dashboards.Persistence;
using Hunting.Web.Dashboards.Runtime;

public sealed class DashboardPageController: IDisposable
{
    private readonly IDashboardRepository _dashboardRepository;
    private readonly DashboardWidgetRunner _widgetRunner;
    private readonly ILogger<DashboardPageController> _logger;
    private readonly Dictionary<string, CancellationTokenSource> _runningWidgets = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _autoRefreshCts;
    private bool _active;

    public DashboardPageController(
        IDashboardRepository dashboardRepository,
        DashboardWidgetRunner widgetRunner,
        ILogger<DashboardPageController> logger)
    {
        _dashboardRepository = dashboardRepository;
        _widgetRunner = widgetRunner;
        _logger = logger;
    }

    public event Action? StateChanged;

    public DashboardPageState State { get; } = new();

    public async Task LoadAsync(string? dashboardId)
    {
        DeactivateInternal();
        _active = true;

        State.ResetForLoad();
        NotifyStateChanged();

        if (string.IsNullOrWhiteSpace(dashboardId))
        {
            State.Error = "Dashboard ID is required.";
            State.Loading = false;
            NotifyStateChanged();
            return;
        }

        try
        {
            State.Dashboard = await _dashboardRepository.GetAsync(dashboardId);
            if (State.Dashboard is null)
            {
                State.Error = "The requested dashboard was not found.";
            }
        }
        catch (Exception ex)
        {
            State.Error = $"Could not load dashboard. {ex.Message}";
            _logger.LogWarning(ex, "Could not load dashboard {DashboardId}.", dashboardId);
        }
        finally
        {
            State.Loading = false;
            NotifyStateChanged();
        }

        if (State.Dashboard is not null)
        {
            await RunAllWidgetsAsync();
        }
    }

    public void Deactivate()
    {
        _active = false;
        DeactivateInternal();
    }

    public async Task RunAllWidgetsAsync()
    {
        if (State.Dashboard is null || !_active)
        {
            return;
        }

        var queryWidgets = State.Dashboard.Widgets
            .Where(DashboardPageState.CanRunWidget)
            .ToArray();

        if (queryWidgets.Length == 0)
        {
            return;
        }

        await Task.WhenAll(queryWidgets.Select(RunWidgetAsync));
    }

    public async Task RunWidgetAsync(DashboardWidgetDefinition widget)
    {
        if (!DashboardPageState.CanRunWidget(widget) || !_active)
        {
            return;
        }

        CancelWidgetRun(widget.Id);

        var cts = new CancellationTokenSource();
        var token = cts.Token;
        _runningWidgets[widget.Id] = cts;

        State.WidgetResults[widget.Id] = new DashboardWidgetRunResult
        {
            WidgetId = widget.Id,
            Status = DashboardWidgetRunStatus.Running,
            StartedAtUtc = DateTime.UtcNow
        };

        NotifyStateChanged();

        try
        {
            var result = await _widgetRunner.RunAsync(widget, token);
            if (IsActiveRun(widget.Id, cts))
            {
                State.WidgetResults[widget.Id] = result;
            }
        }
        catch (OperationCanceledException)
        {
            if (IsActiveRun(widget.Id, cts))
            {
                var startedAtUtc = DateTime.UtcNow;
                State.WidgetResults[widget.Id] = new DashboardWidgetRunResult
                {
                    WidgetId = widget.Id,
                    Status = DashboardWidgetRunStatus.Cancelled,
                    StartedAtUtc = startedAtUtc,
                    Duration = DateTime.UtcNow - startedAtUtc
                };
            }
        }
        finally
        {
            if (IsActiveRun(widget.Id, cts))
            {
                _runningWidgets.Remove(widget.Id);
            }

            cts.Dispose();
            NotifyStateChanged();
        }
    }

    public Task ToggleAutoRefreshAsync()
    {
        if (State.AutoRefreshEnabled)
        {
            StopAutoRefresh();
            State.AutoRefreshEnabled = false;
            NotifyStateChanged();
            return Task.CompletedTask;
        }

        State.AutoRefreshEnabled = true;
        StartAutoRefresh();
        NotifyStateChanged();
        return Task.CompletedTask;
    }

    public void StartEditMode()
    {
        State.EditMode = true;
        NotifyStateChanged();
    }

    public async Task SaveEditModeAsync()
    {
        if (!State.EditMode || State.Dashboard is null)
        {
            return;
        }

        State.SaveError = null;
        var updatedDashboard = State.Dashboard with { UpdatedAtUtc = DateTime.UtcNow };
        var validationErrors = DashboardModelValidator.Validate(updatedDashboard);
        if (validationErrors.Count > 0)
        {
            State.SaveError = string.Join(" ", validationErrors);
            NotifyStateChanged();
            return;
        }

        try
        {
            await _dashboardRepository.SaveAsync(updatedDashboard);
            State.Dashboard = updatedDashboard;
            State.CloseWidgetEditor();
            State.CloseDashboardSettingsEditor();
            State.EditMode = false;

            if (State.AutoRefreshEnabled && !State.CanStartAutoRefresh)
            {
                StopAutoRefresh();
                State.AutoRefreshEnabled = false;
            }
            else
            {
                RestartAutoRefreshIfEnabled();
            }
        }
        catch (Exception ex)
        {
            State.SaveError = $"Could not save dashboard. {ex.Message}";
            _logger.LogWarning(ex, "Could not save dashboard {DashboardId}.", updatedDashboard.Id);
        }

        NotifyStateChanged();
    }

    public void OpenDashboardSettingsEditor()
    {
        if (!State.EditMode)
        {
            return;
        }

        State.SaveError = null;
        State.SettingsEditorOpen = true;
        NotifyStateChanged();
    }

    public void CloseDashboardSettingsEditor()
    {
        State.CloseDashboardSettingsEditor();
        NotifyStateChanged();
    }

    public Task SaveDashboardSettingsAsync(DashboardDefinition dashboard)
    {
        if (!State.EditMode)
        {
            return Task.CompletedTask;
        }

        State.SaveError = null;

        State.Dashboard = dashboard;
        State.CloseDashboardSettingsEditor();
        NotifyStateChanged();
        return Task.CompletedTask;
    }

    public void OpenAddWidgetEditor()
    {
        if (!State.EditMode)
        {
            return;
        }

        State.SaveError = null;
        State.EditingWidget = null;
        State.NextWidgetLayout = GetNextWidgetLayout();
        State.EditorOpen = true;
        NotifyStateChanged();
    }

    public void OpenEditWidgetEditor(DashboardWidgetDefinition widget)
    {
        if (!State.EditMode)
        {
            return;
        }

        State.SaveError = null;
        State.EditingWidget = widget;
        State.NextWidgetLayout = widget.Layout;
        State.EditorOpen = true;
        NotifyStateChanged();
    }

    public void CloseWidgetEditor()
    {
        State.CloseWidgetEditor();
        NotifyStateChanged();
    }

    public async Task SaveWidgetAsync(DashboardWidgetDefinition widget)
    {
        if (!State.EditMode)
        {
            return;
        }

        if (State.Dashboard is null)
        {
            return;
        }

        State.SaveError = null;

        var widgets = State.Dashboard.Widgets.ToList();
        var existingIndex = widgets.FindIndex(candidate => string.Equals(candidate.Id, widget.Id, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            widgets[existingIndex] = widget;
            State.WidgetResults.Remove(widget.Id);
            CancelWidgetRun(widget.Id);
        }
        else
        {
            widgets.Add(widget);
        }

        ApplyDashboardWidgets(widgets);
        if (!string.IsNullOrWhiteSpace(State.SaveError))
        {
            NotifyStateChanged();
            return;
        }

        State.CloseWidgetEditor();
        NotifyStateChanged();

        var savedWidget = State.Dashboard.Widgets.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, widget.Id, StringComparison.OrdinalIgnoreCase));

        if (savedWidget is not null && DashboardPageState.CanRunWidget(savedWidget))
        {
            await RunWidgetAsync(savedWidget);
        }
    }

    public Task DeleteWidgetAsync(DashboardWidgetDefinition widget)
    {
        if (!State.EditMode)
        {
            return Task.CompletedTask;
        }

        if (State.Dashboard is null)
        {
            return Task.CompletedTask;
        }

        State.SaveError = null;
        CancelWidgetRun(widget.Id);
        State.WidgetResults.Remove(widget.Id);

        var widgets = State.Dashboard.Widgets
            .Where(candidate => !string.Equals(candidate.Id, widget.Id, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        ApplyDashboardWidgets(widgets);
        NotifyStateChanged();
        return Task.CompletedTask;
    }

    public Task SaveWidgetLayoutAsync(DashboardWidgetLayoutChange change)
        => SaveWidgetLayoutsAsync([change]);

    public Task SaveWidgetLayoutsAsync(IReadOnlyList<DashboardWidgetLayoutChange> changes)
    {
        if (!State.EditMode)
        {
            return Task.CompletedTask;
        }

        if (State.Dashboard is null || changes.Count == 0)
        {
            return Task.CompletedTask;
        }

        var changesByWidgetId = changes
            .GroupBy(change => change.WidgetId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Layout, StringComparer.OrdinalIgnoreCase);
        var changed = false;
        var widgets = State.Dashboard.Widgets
            .Select(widget =>
            {
                if (!changesByWidgetId.TryGetValue(widget.Id, out var layout))
                {
                    return widget;
                }

                changed = true;
                return widget with { Layout = layout };
            })
            .ToArray();

        if (!changed)
        {
            return Task.CompletedTask;
        }

        State.SaveError = null;
        ApplyDashboardWidgets(widgets);
        NotifyStateChanged();
        return Task.CompletedTask;
    }

    public DashboardExport? BuildDashboardExport()
    {
        if (State.Dashboard is null)
        {
            return null;
        }

        try
        {
            var json = DashboardJsonTransfer.Export(State.Dashboard);
            return new DashboardExport(BuildExportFileName(State.Dashboard), json);
        }
        catch (Exception ex)
        {
            State.SaveError = $"Could not export dashboard. {ex.Message}";
            _logger.LogWarning(ex, "Could not export dashboard {DashboardId}.", State.Dashboard.Id);
            NotifyStateChanged();
            return null;
        }
    }

    public void SetSaveError(string message)
    {
        State.SaveError = message;
        NotifyStateChanged();
    }

    private void StartAutoRefresh()
    {
        StopAutoRefresh();

        if (!State.CanStartAutoRefresh || State.Dashboard?.Refresh.IntervalSeconds is not int intervalSeconds)
        {
            return;
        }

        _autoRefreshCts = new CancellationTokenSource();
        _ = AutoRefreshDashboardLoopAsync(TimeSpan.FromSeconds(intervalSeconds), _autoRefreshCts);
    }

    private async Task AutoRefreshDashboardLoopAsync(TimeSpan interval, CancellationTokenSource cts)
    {
        var token = cts.Token;

        try
        {
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(token))
            {
                await RunAllWidgetsAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when auto-refresh is stopped, the dashboard changes, or the page is deactivated.
        }
        finally
        {
            cts.Dispose();
        }
    }

    private void StopAutoRefresh()
    {
        _autoRefreshCts?.Cancel();
        _autoRefreshCts = null;
    }

    private void RestartAutoRefreshIfEnabled()
    {
        if (!State.AutoRefreshEnabled)
        {
            return;
        }

        StartAutoRefresh();
    }

    private void ApplyDashboardWidgets(IReadOnlyList<DashboardWidgetDefinition> widgets)
    {
        if (State.Dashboard is null)
        {
            return;
        }

        var updatedDashboard = State.Dashboard with { Widgets = widgets };
        var validationErrors = DashboardModelValidator.Validate(updatedDashboard);
        if (validationErrors.Count > 0)
        {
            State.SaveError = string.Join(" ", validationErrors);
            return;
        }

        State.Dashboard = updatedDashboard;
    }

    private DashboardLayout GetNextWidgetLayout()
    {
        if (State.Dashboard?.Widgets.Count is not > 0)
        {
            return new DashboardLayout { X = 0, Y = 0, Width = 4, Height = 3, MinimumWidth = 2, MinimumHeight = 2 };
        }

        var nextY = State.Dashboard.Widgets.Max(widget => widget.Layout.Y + Math.Max(1, widget.Layout.Height));
        return new DashboardLayout { X = 0, Y = nextY, Width = 4, Height = 3, MinimumWidth = 2, MinimumHeight = 2 };
    }

    private bool IsActiveRun(string widgetId, CancellationTokenSource cts)
        => _runningWidgets.TryGetValue(widgetId, out var activeCts)
            && ReferenceEquals(activeCts, cts);

    private void CancelWidgetRun(string widgetId)
    {
        if (_runningWidgets.Remove(widgetId, out var existingCts))
        {
            existingCts.Cancel();
        }
    }

    private void CancelAllWidgetRuns()
    {
        foreach (var (_, cts) in _runningWidgets)
        {
            cts.Cancel();
        }

        _runningWidgets.Clear();
    }

    private void DeactivateInternal()
    {
        StopAutoRefresh();
        CancelAllWidgetRuns();
    }

    private void NotifyStateChanged()
        => StateChanged?.Invoke();

    private static string BuildExportFileName(DashboardDefinition dashboard)
    {
        var safeName = new string(dashboard.Name
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-')
            .ToArray());

        safeName = string.Join('-', safeName.Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "dashboard";
        }

        return $"{safeName}-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
    }

    public void Dispose() => _autoRefreshCts?.Dispose();
}
