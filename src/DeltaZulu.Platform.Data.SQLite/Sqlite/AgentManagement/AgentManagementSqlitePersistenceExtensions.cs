using DeltaZulu.Platform.Data.AgentManagement;
using DeltaZulu.Platform.Data.Sqlite.AgentManagement.Repositories;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace DeltaZulu.Platform.Data.Sqlite.AgentManagement;

public static class AgentManagementSqlitePersistenceExtensions
{
    public static IServiceCollection AddAgentManagementSqlitePersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddSingleton<IAgentManagementDbConnectionFactory>(
            new SqliteAgentManagementConnectionFactory(connectionString));
        services.AddSingleton<IAgentManagementPersistenceBootstrapper>(
            new SqliteAgentManagementBootstrapper(connectionString));

        services.AddScoped(_ => new AgentManagementDapperSession(connectionString));
        services.AddScoped<IAgentManagementUnitOfWork>(sp =>
            sp.GetRequiredService<AgentManagementDapperSession>());

        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<IAgentGroupRepository, AgentGroupRepository>();
        services.AddScoped<IResourceProfileRepository, ResourceProfileRepository>();
        services.AddScoped<IResourceProfileVersionRepository, ResourceProfileVersionRepository>();
        services.AddScoped<IDaemonConfigPolicyRepository, DaemonConfigPolicyRepository>();
        services.AddScoped<IDaemonConfigVersionRepository, DaemonConfigVersionRepository>();
        services.AddScoped<IPolicyAssignmentRepository, PolicyAssignmentRepository>();

        return services;
    }
}
