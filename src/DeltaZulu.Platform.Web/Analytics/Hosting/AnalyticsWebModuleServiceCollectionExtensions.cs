using DeltaZulu.Platform.Application.Analytics.Nrt;
using DeltaZulu.Platform.Data.Proton;
using DeltaZulu.Platform.Application.Analytics.Validation;
using DeltaZulu.Platform.Data.DuckDb.Execution;
using DeltaZulu.Platform.Data.DuckDb.Analytics;
using DeltaZulu.Platform.Data.DuckDb.Ingestion;
using DeltaZulu.Platform.Data.DuckDb;
using DeltaZulu.Platform.Domain.Analytics.Observability;
using DeltaZulu.Platform.Data.Sqlite.Analytics;
using DeltaZulu.Platform.Domain.Analytics.Catalog;
using DeltaZulu.Platform.Domain.Analytics.Execution;
using DeltaZulu.Platform.Domain.Analytics.Investigations;
using DeltaZulu.Platform.Domain.Analytics.Schema;
using DeltaZulu.Platform.Domain.Governance.Contracts;
using DeltaZulu.Platform.Web.Analytics.Dashboards;
using DeltaZulu.Platform.Web.Analytics.Dashboards.DependencyInjection;
using DeltaZulu.Platform.Web.Analytics.Dashboards.PageState;
using DeltaZulu.Platform.Web.Analytics.Library;
using DeltaZulu.Platform.Web.Analytics.Rendering;
using DeltaZulu.Platform.Web.Analytics.Services;
using MudBlazor.Services;

namespace DeltaZulu.Platform.Web.Analytics.Hosting;

public static class AnalyticsWebModuleServiceCollectionExtensions
{
    private static readonly string[] AppStateTables = [
        "user_settings",
        "saved_queries",
        "query_history",
        "visualizations",
        "detections",
        "detection_runs",
        "alerts",
        "alert_entities",
        "incident_candidates",
        "candidate_alert_links",
        "candidate_evidence",
        "source_observations"];

    /// <summary>
    /// Registers Analytics query/runtime services. This layer is reusable outside the
    /// standalone Blazor host and deliberately excludes application-state persistence and UI providers.
    /// </summary>
    public static IServiceCollection AddAnalyticsRuntime(
        this IServiceCollection services,
        AnalyticsModuleOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(_ => {
            var catalog = new ApprovedViewCatalog();
            catalog.RegisterAll(SchemaConventions.CanonicalViews);
            return catalog;
        });

        services.AddSingleton<IQuerySyntaxValidator, KqlQuerySyntaxValidator>();
        services.AddSingleton(_ => new DuckDbConnectionFactory(
            $"DataSource={options.DuckDbPath}",
            attachedDatabases: [new DuckDbAttachedDatabase(
                options.AppDatabaseAlias,
                options.AppDbPath,
                readOnly: true,
                views: CreateAppStateViews(options.AppViewSchema))]));
        services.AddSingleton<SchemaApplier>();
        services.AddSingleton(sp => new QueryRuntime(
            sp.GetRequiredService<ApprovedViewCatalog>(),
            sp.GetRequiredService<DuckDbConnectionFactory>(),
            defaultLimit: options.DefaultLimit,
            timeoutSeconds: options.TimeoutSeconds,
            developerMode: options.DeveloperMode,
            plannerMaxIterations: options.PlannerMaxIterations));
        services.AddSingleton<IAnalyticsQueryExecutor, AnalyticsQueryExecutor>();
        services.AddSingleton<DuckDbSourceObservationWriter>();
        services.AddSingleton<DuckDbAgentObservationWriter>();
        services.AddSingleton<IOperationalMetricsReader, DuckDbOperationalMetricsReader>();
        services.AddSingleton<ISourceMetricsReader, DuckDbSourceMetricsReader>();
        services.AddSingleton<QueryService>();

        return services;
    }

    /// <summary>
    /// Registers local Analytics application-state persistence and stateful services that depend on it.
    /// This path-based SQLite bridge is currently supplied by the platform composition root and can
    /// be replaced with tenant/module-aware persistence later.
    /// </summary>
    public static IServiceCollection AddAnalyticsApplicationState(
        this IServiceCollection services,
        AnalyticsModuleOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        // Keep repository writes on Microsoft.Data.Sqlite: the analytics runtime attachment supports
        // cross-database reads, but does not support SQLite-backed ON CONFLICT/MERGE writes.
        services.AddApplicationPersistence($"Data Source={options.AppDbPath}");
        services.AddDashboards();
        services.AddScoped<UserSettingsState>();
        services.AddScoped<QueryLibraryService>();
        services.AddScoped<VisualizationLibraryService>();
        services.AddScoped<LibraryService>();
        services.AddScoped<InvestigationService>();
        services.AddScoped<LibraryPageController>();
        services.AddScoped<DashboardListPageController>();

        return services;
    }

    /// <summary>
    /// Registers Analytics web module services for platform composition.
    /// Route manifest, navigation entries, and static assets are exposed separately by
    /// <see cref="DeltaZulu.Platform.Web.Analytics.AnalyticsModule" /> through shared
    /// <c>DeltaZulu.Platform.Web.Platform</c> contracts.
    /// </summary>
    public static IServiceCollection AddAnalyticsWebModule(
        this IServiceCollection services,
        AnalyticsModuleOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        if (options.RegisterMudServices)
        {
            services.AddMudServices();
        }

        services.AddAnalyticsRuntime(options);
        services.AddAnalyticsApplicationState(options);
        services.AddAnalyticsRenderWeb();
        services.AddScoped<EditorBus>();
        services.AddScoped<LanguageService>();
        services.AddScoped<WidgetEditorInterop>();
        services.AddScoped<DashboardTransferInterop>();
        services.AddScoped<MitreAttackCatalogService>();

        services.AddProtonDetectionBackend();
        services.AddSingleton<NrtRuleCompiler>();
        services.AddScoped<NrtRuleService>();

        return services;
    }

    private static IReadOnlyList<DuckDbAttachedView> CreateAppStateViews(string appViewSchema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appViewSchema);
        return AppStateTables
            .Select(tableName => new DuckDbAttachedView(tableName, tableName, appViewSchema))
            .ToArray();
    }
}
