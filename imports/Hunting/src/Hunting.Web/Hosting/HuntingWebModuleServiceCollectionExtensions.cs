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
    /// Registers DuckDB-backed Hunting query/runtime services. This layer is reusable outside the
    /// standalone Blazor host and deliberately excludes application-state persistence and UI providers.
    /// </summary>
    public static IServiceCollection AddHuntingRuntime(
        this IServiceCollection services,
        HuntingWebModuleOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

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

        return services;
    }

    /// <summary>
    /// Registers local Hunting application-state persistence and stateful services that depend on it.
    /// This is a standalone-compatible bridge; a platform host should replace the path-based SQLite
    /// ownership with tenant/module-aware persistence before final mounting.
    /// </summary>
    public static IServiceCollection AddHuntingApplicationState(
        this IServiceCollection services,
        HuntingWebModuleOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddApplicationPersistence($"Data Source={options.AppDbPath}");
        services.AddDashboards();
        services.AddScoped<UserSettingsState>();
        services.AddScoped<QueryLibraryService>();
        services.AddScoped<VisualizationLibraryService>();
        services.AddScoped<LibraryService>();
        services.AddScoped<LibraryPageController>();
        services.AddScoped<DashboardListPageController>();

        return services;
    }

    /// <summary>
    /// Registers the current Hunting web module services for standalone hosting or early platform composition.
    /// This is not the final platform module contract: before production mounting, the route manifest,
    /// navigation entries, static assets, provider ownership, and persistence ownership should move behind
    /// shared <c>DeltaZulu.Platform.Web.Abstractions</c> contracts.
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

        services.AddHuntingRuntime(options);
        services.AddHuntingApplicationState(options);
        services.AddHuntingRenderWeb();
        services.AddScoped<EditorBus>();
        services.AddScoped<LanguageService>();

        return services;
    }
}
