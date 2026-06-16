using DeltaZulu.Platform.Web.Analytics.Dashboards.PageState;
using DeltaZulu.Platform.Web.Analytics.Dashboards.Persistence;
using DeltaZulu.Platform.Web.Analytics.Dashboards.Runtime;

namespace DeltaZulu.Platform.Web.Analytics.Dashboards.DependencyInjection;

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