namespace DeltaZulu.Hunting.Web.Dashboards.PageState;

using DeltaZulu.Hunting.Web.Dashboards.Persistence;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

public sealed class DashboardListPageController
{
    private readonly IDashboardRepository _dashboardRepository;
    private readonly IJSRuntime _js;
    private readonly NavigationManager _navigation;

    public DashboardListPageController(
        IDashboardRepository dashboardRepository,
        IJSRuntime js,
        NavigationManager navigation)
    {
        _dashboardRepository = dashboardRepository ?? throw new ArgumentNullException(nameof(dashboardRepository));
        _js = js ?? throw new ArgumentNullException(nameof(js));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
    }

    public DashboardListPageState State { get; } = new();

    public async Task LoadDashboardsAsync(CancellationToken cancellationToken = default)
    {
        State.Loading = true;
        State.Error = null;

        try
        {
            var dashboards = await _dashboardRepository.ListAsync(cancellationToken);
            State.Dashboards.Clear();
            State.Dashboards.AddRange(dashboards.OrderByDescending(dashboard => dashboard.UpdatedAtUtc));
            State.ClampPage();
        }
        catch (Exception ex)
        {
            State.Error = $"Could not load dashboards. {ex.Message}";
        }
        finally
        {
            State.Loading = false;
        }
    }

    public async Task CreateDashboardAsync(CancellationToken cancellationToken = default)
    {
        State.Error = null;

        var now = DateTime.UtcNow;
        var dashboard = new DashboardDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "New dashboard",
            Description = "Local dashboard",
            Refresh = DashboardRefreshPolicy.Manual(),
            Widgets = [],
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        try
        {
            await _dashboardRepository.SaveAsync(dashboard, cancellationToken);
            _navigation.NavigateTo($"/dashboards/{dashboard.Id}");
        }
        catch (Exception ex)
        {
            State.Error = $"Could not create dashboard. {ex.Message}";
        }
    }

    public async Task ImportDashboardAsync(CancellationToken cancellationToken = default)
    {
        State.Error = null;

        try
        {
            var json = await _js.InvokeAsync<string?>("huntingDashboardTransfer.pickJson", cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            var dashboard = DashboardJsonTransfer.ImportAsCopy(json);
            await _dashboardRepository.SaveAsync(dashboard, cancellationToken);
            _navigation.NavigateTo($"/dashboards/{dashboard.Id}");
        }
        catch (Exception ex)
        {
            State.Error = $"Could not import dashboard. {ex.Message}";
        }
    }

    public async Task DeleteDashboardAsync(string dashboardId, CancellationToken cancellationToken = default)
    {
        State.Error = null;

        try
        {
            await _dashboardRepository.DeleteAsync(dashboardId, cancellationToken);
            await LoadDashboardsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            State.Error = $"Could not delete dashboard. {ex.Message}";
        }
    }

    public void SetSearchText(string? searchText)
    {
        State.SearchText = searchText ?? string.Empty;
        State.Page = 1;
    }

    public void ClearSearch()
    {
        State.SearchText = string.Empty;
        State.Page = 1;
    }

    public void PreviousPage()
    {
        if (State.Page > 1)
        {
            State.Page--;
        }
    }

    public void NextPage()
    {
        if (State.Page < State.TotalPages)
        {
            State.Page++;
        }
    }

    public void OpenDashboard(string dashboardId)
        => _navigation.NavigateTo($"/dashboards/{dashboardId}");
}