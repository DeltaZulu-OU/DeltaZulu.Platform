namespace Hunting.Web.Dashboards.Persistence;

using Hunting.Web.Dashboards;

public interface IDashboardRepository
{
    Task<IReadOnlyList<DashboardSummary>> ListAsync(CancellationToken ct = default);
    Task<DashboardDefinition?> GetAsync(string id, CancellationToken ct = default);
    Task SaveAsync(DashboardDefinition dashboard, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}
