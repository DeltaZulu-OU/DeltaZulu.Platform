using Microsoft.Extensions.DependencyInjection;
using Workbench.Application.Abstractions;
using Workbench.Persistence.Repositories;

namespace Workbench.Persistence;

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
        return services;
    }
}
