namespace DeltaZulu.Platform.Web.Hunting.Dashboards.DependencyInjection;

using DeltaZulu.Platform.Web.Hunting.Dashboards.PageState;
using DeltaZulu.Platform.Web.Hunting.Dashboards.Persistence;
using DeltaZulu.Platform.Web.Hunting.Dashboards.Runtime;
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