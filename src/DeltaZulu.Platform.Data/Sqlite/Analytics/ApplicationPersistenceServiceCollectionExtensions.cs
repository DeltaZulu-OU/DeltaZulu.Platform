
using DeltaZulu.Platform.Data.Seeding;
using DeltaZulu.Platform.Data.Sqlite.Analytics.AlertEntities;
using DeltaZulu.Platform.Data.Sqlite.Analytics.Alerts;
using DeltaZulu.Platform.Data.Sqlite.Analytics.Candidates;
using DeltaZulu.Platform.Data.Sqlite.Analytics.CuratedAnalytics;
using DeltaZulu.Platform.Data.Sqlite.Analytics.DetectionRuns;
using DeltaZulu.Platform.Data.Sqlite.Analytics.Detections;
using DeltaZulu.Platform.Data.Sqlite.Analytics.QueryHistory;
using DeltaZulu.Platform.Data.Sqlite.Analytics.SavedQueries;
using DeltaZulu.Platform.Data.Sqlite.Analytics.Settings;
using DeltaZulu.Platform.Data.Sqlite.Analytics.Visualizations;
using DeltaZulu.Platform.Domain.Analytics.AlertEntities;
using DeltaZulu.Platform.Domain.Analytics.Alerts;
using DeltaZulu.Platform.Domain.Analytics.Candidates;
using DeltaZulu.Platform.Domain.Analytics.CuratedAnalytics;
using DeltaZulu.Platform.Domain.Analytics.DetectionRuns;
using DeltaZulu.Platform.Domain.Analytics.Detections;
using DeltaZulu.Platform.Domain.Analytics.QueryHistory;
using DeltaZulu.Platform.Domain.Analytics.SavedQueries;
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

    private static void AddApplicationRepositories(IServiceCollection services)
    {
        services.AddSingleton<IUserSettingsRepository, DapperUserSettingsRepository>();
        services.AddSingleton<ISavedQueryRepository, DapperSavedQueryRepository>();
        services.AddSingleton<ICuratedAnalyticRepository, DapperCuratedAnalyticRepository>();
        services.AddSingleton<IQueryHistoryRepository, DapperQueryHistoryRepository>();
        services.AddSingleton<IVisualizationRepository, DapperVisualizationRepository>();
        services.AddSingleton<IDetectionRecordRepository, DapperDetectionRepository>();
        services.AddSingleton<IDetectionRunRepository, DapperDetectionRunRepository>();
        services.AddSingleton<IAlertRepository, DapperAlertRepository>();
        services.AddSingleton<IAlertEntityRepository, DapperAlertEntityRepository>();
        services.AddSingleton<IIncidentCandidateRepository, DapperIncidentCandidateRepository>();
        services.AddSingleton<ICandidateEvidenceRepository, DapperCandidateEvidenceRepository>();
    }

    public static async Task InitializeApplicationPersistenceAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var settings = services.GetRequiredService<IUserSettingsRepository>();
        await settings.EnsureInitializedAsync(cancellationToken);

        var savedQueries = services.GetRequiredService<ISavedQueryRepository>();
        await savedQueries.EnsureInitializedAsync(cancellationToken);
        await SampleSavedQuerySeeder.SeedMissingAsync(savedQueries, cancellationToken);

        var curatedAnalytics = services.GetRequiredService<ICuratedAnalyticRepository>();
        await curatedAnalytics.EnsureInitializedAsync(cancellationToken);

        var queryHistory = services.GetRequiredService<IQueryHistoryRepository>();
        await queryHistory.EnsureInitializedAsync(cancellationToken);

        var visualizations = services.GetRequiredService<IVisualizationRepository>();
        await visualizations.EnsureInitializedAsync(cancellationToken);

        var detections = services.GetRequiredService<IDetectionRecordRepository>();
        await detections.EnsureInitializedAsync(cancellationToken);

        var detectionRuns = services.GetRequiredService<IDetectionRunRepository>();
        await detectionRuns.EnsureInitializedAsync(cancellationToken);

        var alerts = services.GetRequiredService<IAlertRepository>();
        await alerts.EnsureInitializedAsync(cancellationToken);

        var alertEntities = services.GetRequiredService<IAlertEntityRepository>();
        await alertEntities.EnsureInitializedAsync(cancellationToken);

        var candidates = services.GetRequiredService<IIncidentCandidateRepository>();
        await candidates.EnsureInitializedAsync(cancellationToken);

        var candidateEvidence = services.GetRequiredService<ICandidateEvidenceRepository>();
        await candidateEvidence.EnsureInitializedAsync(cancellationToken);
    }
}