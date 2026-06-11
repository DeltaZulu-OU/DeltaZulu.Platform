namespace DeltaZulu.Platform.Web.Hunting.Hosting;

using DeltaZulu.Platform.Domain.Hunting.Catalog;
using DeltaZulu.Platform.Data.Hunting;
using DeltaZulu.Platform.Data.Hunting.Persistence;
using DeltaZulu.Platform.Domain.Hunting.Schema;
using DeltaZulu.Platform.Web.Hunting.Dashboards.DependencyInjection;
using DeltaZulu.Platform.Web.Hunting.Dashboards.PageState;
using DeltaZulu.Platform.Web.Hunting.Library;
using DeltaZulu.Platform.Web.Hunting.Rendering;
using DeltaZulu.Platform.Web.Hunting.Services;
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
            catalog.RegisterAll(SchemaConventions.CanonicalViews);
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
    /// This path-based SQLite bridge is currently supplied by the platform composition root and can
    /// be replaced with tenant/module-aware persistence later.
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
    /// Registers Hunting web module services for platform composition.
    /// Route manifest, navigation entries, and static assets are exposed separately by
    /// <see cref="DeltaZulu.Platform.Web.Hunting.HuntingModule" /> through shared
    /// <c>DeltaZulu.Platform.Web.Platform</c> contracts.
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