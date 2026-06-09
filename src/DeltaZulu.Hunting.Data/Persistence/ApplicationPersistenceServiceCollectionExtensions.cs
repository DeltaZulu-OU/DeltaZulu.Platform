namespace DeltaZulu.Hunting.Data.Persistence;

using DeltaZulu.Hunting.Application.Alerts;
using DeltaZulu.Hunting.Application.DetectionRuns;
using DeltaZulu.Hunting.Application.Detections;
using DeltaZulu.Hunting.Application.QueryHistory;
using DeltaZulu.Hunting.Application.SavedQueries;
using DeltaZulu.Hunting.Application.Visualizations;
using DeltaZulu.Hunting.Data.Alerts;
using DeltaZulu.Hunting.Data.DetectionRuns;
using DeltaZulu.Hunting.Data.Detections;
using DeltaZulu.Hunting.Data.QueryHistory;
using DeltaZulu.Hunting.Data.SavedQueries;
using DeltaZulu.Hunting.Data.Settings;
using DeltaZulu.Hunting.Data.Visualizations;
using Microsoft.Extensions.DependencyInjection;
using IUserSettingsRepository = Application.Settings.IUserSettingsRepository;

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
        services.AddSingleton<IUserSettingsRepository, DapperUserSettingsRepository>();
        services.AddSingleton<ISavedQueryRepository, DapperSavedQueryRepository>();
        services.AddSingleton<IQueryHistoryRepository, DapperQueryHistoryRepository>();
        services.AddSingleton<IVisualizationRepository, DapperVisualizationRepository>();
        services.AddSingleton<IDetectionRepository, DapperDetectionRepository>();
        services.AddSingleton<IDetectionRunRepository, DapperDetectionRunRepository>();
        services.AddSingleton<IAlertRepository, DapperAlertRepository>();

        return services;
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

        var queryHistory = services.GetRequiredService<IQueryHistoryRepository>();
        await queryHistory.EnsureInitializedAsync(cancellationToken);

        var visualizations = services.GetRequiredService<IVisualizationRepository>();
        await visualizations.EnsureInitializedAsync(cancellationToken);

        var detections = services.GetRequiredService<IDetectionRepository>();
        await detections.EnsureInitializedAsync(cancellationToken);

        var detectionRuns = services.GetRequiredService<IDetectionRunRepository>();
        await detectionRuns.EnsureInitializedAsync(cancellationToken);

        var alerts = services.GetRequiredService<IAlertRepository>();
        await alerts.EnsureInitializedAsync(cancellationToken);
    }
}
