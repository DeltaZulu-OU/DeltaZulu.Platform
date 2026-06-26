using DeltaZulu.Platform.Data.Seeding;
using DeltaZulu.Platform.Data.Sqlite.Analytics.AlertEntities;
using DeltaZulu.Platform.Data.Sqlite.Analytics.Alerts;
using DeltaZulu.Platform.Data.Sqlite.Analytics.Candidates;
using DeltaZulu.Platform.Data.Sqlite.Analytics.CuratedAnalytics;
using DeltaZulu.Platform.Data.Sqlite.Analytics.DetectionRuns;
using DeltaZulu.Platform.Data.Sqlite.Analytics.Detections;
using DeltaZulu.Platform.Data.Sqlite.Analytics.Nrt;
using DeltaZulu.Platform.Data.Sqlite.Analytics.QueryHistory;
using DeltaZulu.Platform.Data.Sqlite.Analytics.SavedQueries;
using DeltaZulu.Platform.Data.Sqlite.Analytics.Scheduled;
using DeltaZulu.Platform.Data.Sqlite.Analytics.Settings;
using DeltaZulu.Platform.Data.Sqlite.Analytics.Visualizations;
using DeltaZulu.Platform.Domain.Analytics.AlertEntities;
using DeltaZulu.Platform.Domain.Analytics.Alerts;
using DeltaZulu.Platform.Domain.Analytics.Candidates;
using DeltaZulu.Platform.Domain.Analytics.CuratedAnalytics;
using DeltaZulu.Platform.Domain.Analytics.DetectionRuns;
using DeltaZulu.Platform.Domain.Analytics.Detections;
using DeltaZulu.Platform.Domain.Analytics.Nrt;
using DeltaZulu.Platform.Domain.Analytics.QueryHistory;
using DeltaZulu.Platform.Domain.Analytics.SavedQueries;
using DeltaZulu.Platform.Domain.Analytics.Scheduled;
using DeltaZulu.Platform.Domain.Analytics.Visualizations;
using Microsoft.Extensions.DependencyInjection;
using IUserSettingsRepository = DeltaZulu.Platform.Domain.Analytics.Settings.IUserSettingsRepository;

namespace DeltaZulu.Platform.Data.Sqlite.Analytics;

public static class ApplicationPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationPersistence(
        this IServiceCollection services,
        string sqliteConnectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(sqliteConnectionString);

        services.AddSingleton<IAppDbConnectionFactory>(
            _ => new SqliteAppDbConnectionFactory(sqliteConnectionString));
        AddApplicationRepositories(services);

        return services;
    }

    /// <summary>
    /// Registers the operations SQLite database connection factory and the repositories that
    /// own mutable incident-candidate lifecycle state. Separate from the analytics app-state
    /// database so alert lake and operations state stay in distinct files.
    /// </summary>
    public static IServiceCollection AddOperationsPersistence(
        this IServiceCollection services,
        string operationsConnectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationsConnectionString);

        services.AddSingleton<IOperationsDbConnectionFactory>(
            _ => new SqliteOperationsDbConnectionFactory(operationsConnectionString));
        AddApplicationRepository<IIncidentCandidateRepository, DapperIncidentCandidateRepository>(services);
        AddApplicationRepository<ICandidateEvidenceRepository, DapperCandidateEvidenceRepository>(services);

        return services;
    }

    private static void AddApplicationRepositories(IServiceCollection services)
    {
        AddApplicationRepository<IUserSettingsRepository, DapperUserSettingsRepository>(services);
        AddApplicationRepository<ISavedQueryRepository, DapperSavedQueryRepository>(services);
        AddApplicationRepository<ICuratedAnalyticRepository, DapperCuratedAnalyticRepository>(services);
        AddApplicationRepository<IQueryHistoryRepository, DapperQueryHistoryRepository>(services);
        AddApplicationRepository<IVisualizationRepository, DapperVisualizationRepository>(services);
        AddApplicationRepository<IDetectionRecordRepository, DapperDetectionRepository>(services);
        AddApplicationRepository<IDetectionRunRepository, DapperDetectionRunRepository>(services);
        AddApplicationRepository<IAlertRepository, DapperAlertRepository>(services);
        AddApplicationRepository<IAlertEntityRepository, DapperAlertEntityRepository>(services);
        AddApplicationRepository<INrtRuleRepository, DapperNrtRuleRepository>(services);
        AddApplicationRepository<IScheduledDetectionRuleRepository, DapperScheduledDetectionRuleRepository>(services);
    }

    private static void AddApplicationRepository<TRepository, TImplementation>(IServiceCollection services)
        where TRepository : class
        where TImplementation : class, TRepository, IApplicationPersistenceRepository
    {
        services.AddSingleton<TRepository, TImplementation>();
        services.AddSingleton<IApplicationPersistenceRepository>(sp =>
            (IApplicationPersistenceRepository)sp.GetRequiredService<TRepository>());
    }

    public static async Task InitializeApplicationPersistenceAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (var repository in services.GetServices<IApplicationPersistenceRepository>())
        {
            await repository.EnsureInitializedAsync(cancellationToken);
        }

        var savedQueries = services.GetRequiredService<ISavedQueryRepository>();
        await SampleSavedQuerySeeder.SeedMissingAsync(savedQueries, cancellationToken);
    }
}