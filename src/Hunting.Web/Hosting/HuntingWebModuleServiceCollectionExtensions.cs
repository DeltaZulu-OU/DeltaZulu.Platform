namespace Hunting.Web.Hosting;

using Hunting.Core.Catalog;
using Hunting.Data;
using Hunting.Data.Persistence;
using Hunting.Schema;
using Hunting.Web.Dashboards.DependencyInjection;
using Hunting.Web.Dashboards.PageState;
using Hunting.Web.Library;
using Hunting.Web.Rendering;
using Hunting.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

public static class HuntingWebModuleServiceCollectionExtensions
{
    /// <summary>
    /// Registers the current Hunting web module services for standalone hosting or early platform composition.
    /// This is not the final platform module contract: before production mounting, split runtime/query services,
    /// application-state services, and standalone persistence defaults behind shared platform abstractions.
    /// </summary>
    public static IServiceCollection AddHuntingWebModule(
        this IServiceCollection services,
        HuntingWebModuleOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        if (options.RegisterMudServices)
        {
            services.AddMudServices();
        }

        services.AddSingleton(_ => {
            var catalog = new ApprovedViewCatalog();
            SchemaConventions.RegisterCanonicalViews(catalog);
            return catalog;
        });

        services.AddSingleton(_ => new DuckDbConnectionFactory($"DataSource={options.DuckDbPath}"));
        services.AddSingleton<SchemaApplier>();
        services.AddSingleton(sp => new QueryRuntime(
            sp.GetRequiredService<ApprovedViewCatalog>(),
            sp.GetRequiredService<DuckDbConnectionFactory>(),
            defaultLimit: options.DefaultLimit,
            timeoutSeconds: options.TimeoutSeconds,
            developerMode: options.DeveloperMode,
            plannerMaxIterations: options.PlannerMaxIterations));

        services.AddSingleton<QueryService>();
        services.AddScoped<EditorBus>();
        services.AddScoped<LanguageService>();
        // Standalone-compatible default. A platform host should supply tenant/module-aware
        // persistence ownership before final mounting instead of relying on a module-local path.
        services.AddApplicationPersistence($"Data Source={options.AppDbPath}");
        services.AddDashboards();
        services.AddHuntingRenderWeb();
        services.AddScoped<UserSettingsState>();
        services.AddScoped<QueryLibraryService>();
        services.AddScoped<VisualizationLibraryService>();
        services.AddScoped<LibraryService>();
        services.AddScoped<LibraryPageController>();
        services.AddScoped<DashboardListPageController>();

        return services;
    }
}
