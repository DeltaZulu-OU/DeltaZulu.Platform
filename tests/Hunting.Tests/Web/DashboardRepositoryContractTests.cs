namespace Hunting.Tests.Web;

using Hunting.Web.Dashboards;
using Hunting.Web.Dashboards.Persistence;

[TestClass]
public sealed class DashboardRepositoryContractTests
{
    [TestMethod]
    public async Task RepositoryContract_SaveGetListDelete_WorksWithInMemoryImplementation()
    {
        IDashboardRepository repository = new InMemoryDashboardRepository();
        var dashboard = new DashboardDefinition
        {
            Name = "SOC overview",
            Widgets =
            [
                new DashboardWidgetDefinition
                {
                    Title = "Events",
                    QueryText = "ProcessEvent | take 5"
                }
            ]
        };
        await repository.SaveAsync(dashboard, TestContext.CancellationToken);
        var loaded = await repository.GetAsync(dashboard.Id, TestContext.CancellationToken);
        var summaries = await repository.ListAsync(TestContext.CancellationToken);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(dashboard.Id, loaded.Id);
        Assert.HasCount(1, summaries);
        Assert.AreEqual(dashboard.Id, summaries[0].Id);
        Assert.AreEqual(1, summaries[0].WidgetCount);
        await repository.DeleteAsync(dashboard.Id, TestContext.CancellationToken);
        Assert.IsNull(await repository.GetAsync(dashboard.Id, TestContext.CancellationToken));
        Assert.IsEmpty(await repository.ListAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task RepositoryContract_GetMissing_ReturnsNull()
    {
        IDashboardRepository repository = new InMemoryDashboardRepository();
        var dashboard = await repository.GetAsync("missing", TestContext.CancellationToken);
        Assert.IsNull(dashboard);
    }

    [TestMethod]
    public async Task RepositoryContract_SaveInvalidDashboard_ThrowsRepositoryException()
    {
        IDashboardRepository repository = new InMemoryDashboardRepository();
        var invalid = new DashboardDefinition
        {
            Name = " "
        };
        await Assert.ThrowsExactlyAsync<DashboardRepositoryException>(() => repository.SaveAsync(invalid, TestContext.CancellationToken));
    }

    private sealed class InMemoryDashboardRepository : IDashboardRepository
    {
        private readonly Dictionary<string, DashboardDefinition> _dashboards = new(StringComparer.OrdinalIgnoreCase);

        public Task DeleteAsync(string id, CancellationToken ct = default)
        {
            _dashboards.Remove(id);
            return Task.CompletedTask;
        }

        public Task<DashboardDefinition?> GetAsync(string id, CancellationToken ct = default)
        {
            _dashboards.TryGetValue(id, out var dashboard);
            return Task.FromResult<DashboardDefinition?>(dashboard);
        }

        public Task<IReadOnlyList<DashboardSummary>> ListAsync(CancellationToken ct = default)
        {
            IReadOnlyList<DashboardSummary> summaries = _dashboards.Values
                .OrderBy(dashboard => dashboard.Name, StringComparer.OrdinalIgnoreCase)
                .Select(dashboard => new DashboardSummary
                {
                    Id = dashboard.Id,
                    Name = dashboard.Name,
                    Description = dashboard.Description,
                    WidgetCount = dashboard.Widgets.Count,
                    CreatedAtUtc = dashboard.CreatedAtUtc,
                    UpdatedAtUtc = dashboard.UpdatedAtUtc
                })
                .ToArray();
            return Task.FromResult(summaries);
        }

        public Task SaveAsync(DashboardDefinition dashboard, CancellationToken ct = default)
        {
            var errors = DashboardModelValidator.Validate(dashboard);
            if (errors.Count > 0)
            {
                throw new DashboardRepositoryException(string.Join(Environment.NewLine, errors));
            }
            _dashboards[dashboard.Id] = dashboard;
            return Task.CompletedTask;
        }
    }

    public TestContext TestContext { get; set; }
}