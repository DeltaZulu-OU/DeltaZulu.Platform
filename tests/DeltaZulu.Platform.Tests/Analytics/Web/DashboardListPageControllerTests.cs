using DeltaZulu.Platform.Web.Analytics.Dashboards;
using DeltaZulu.Platform.Web.Analytics.Dashboards.PageState;
using DeltaZulu.Platform.Web.Analytics.Dashboards.Persistence;
using Microsoft.AspNetCore.Components;

namespace DeltaZulu.Platform.Tests.Analytics.Web;

[TestClass]
public sealed class DashboardListPageControllerTests
{
    [TestMethod]
    public async Task CreateDashboardAsync_NavigatesToAnalyticsDashboardDetailRoute()
    {
        var repository = new InMemoryDashboardRepository();
        var navigation = new TestNavigationManager();
        var controller = CreateController(repository, navigation);

        await controller.CreateDashboardAsync(TestContext.CancellationToken);

        Assert.HasCount(1, repository.SavedDashboards);
        var savedDashboard = repository.SavedDashboards[0];
        Assert.AreEqual($"https://localhost/analytics/dashboards/{savedDashboard.Id}", navigation.Uri);
    }

    [TestMethod]
    public void OpenDashboard_NavigatesToAnalyticsDashboardDetailRoute()
    {
        var navigation = new TestNavigationManager();
        var controller = CreateController(new InMemoryDashboardRepository(), navigation);

        controller.OpenDashboard("dashboard-1");

        Assert.AreEqual("https://localhost/analytics/dashboards/dashboard-1", navigation.Uri);
    }

    public TestContext TestContext { get; set; } = null!;

    private static DashboardListPageController CreateController(
        InMemoryDashboardRepository repository,
        TestNavigationManager navigation)
        => new(repository, new DashboardTransferInterop(null!), navigation);

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager()
        {
            Initialize("https://localhost/", "https://localhost/analytics/dashboards");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
            => Uri = ToAbsoluteUri(uri).ToString();

        protected override void NavigateToCore(string uri, NavigationOptions options)
            => Uri = ToAbsoluteUri(uri).ToString();
    }

    private sealed class InMemoryDashboardRepository : IDashboardRepository
    {
        private readonly Dictionary<string, DashboardDefinition> _dashboards = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<DashboardDefinition> SavedDashboards => _dashboards.Values.ToArray();

        public Task<IReadOnlyList<DashboardSummary>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DashboardSummary>>(
                _dashboards.Values
                    .Select(dashboard => new DashboardSummary {
                        Id = dashboard.Id,
                        Name = dashboard.Name,
                        Description = dashboard.Description,
                        WidgetCount = dashboard.Widgets.Count,
                        CreatedAtUtc = dashboard.CreatedAtUtc,
                        UpdatedAtUtc = dashboard.UpdatedAtUtc
                    })
                    .ToArray());

        public Task<DashboardDefinition?> GetAsync(string id, CancellationToken ct = default)
            => Task.FromResult(_dashboards.GetValueOrDefault(id));

        public Task SaveAsync(DashboardDefinition dashboard, CancellationToken ct = default)
        {
            _dashboards[dashboard.Id] = dashboard;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id, CancellationToken ct = default)
        {
            _dashboards.Remove(id);
            return Task.CompletedTask;
        }
    }
}
