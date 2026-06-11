using DeltaZulu.Platform.Application.Workbench.Abstractions;
using DeltaZulu.Platform.Data.Workbench.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace DeltaZulu.Platform.Data.Workbench;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddWorkbenchPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        SchemaInitializer.Initialize(connectionString);

        services.AddScoped(_ => new DapperSession(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<DapperSession>());
        services.AddScoped<IDetectionRepository, DetectionRepository>();
        services.AddScoped<IIssueRepository, IssueRepository>();
        services.AddScoped<IChangeRequestRepository, ChangeRequestRepository>();
        services.AddScoped<IDetectionVersionRepository, DetectionVersionRepository>();
        services.AddScoped<IMergeIntentRepository, MergeIntentRepository>();
        return services;
    }
}