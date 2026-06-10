namespace DeltaZulu.Hunting.Web.Dashboards.DependencyInjection;

using DeltaZulu.Hunting.Web.Dashboards.PageState;
using DeltaZulu.Hunting.Web.Dashboards.Persistence;
using DeltaZulu.Hunting.Web.Dashboards.Runtime;
using Microsoft.Extensions.DependencyInjection;

public static class DashboardServiceCollectionExtensions
{
    public static IServiceCollection AddDashboards(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IDashboardRepository, SqliteDashboardRepository>();
        services.AddScoped<DashboardWidgetRunner>();
        services.AddScoped<DashboardPageController>();
        return services;
    }
}