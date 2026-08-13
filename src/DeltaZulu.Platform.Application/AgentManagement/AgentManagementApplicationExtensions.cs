using DeltaZulu.Platform.Application.AgentManagement.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DeltaZulu.Platform.Application.AgentManagement;

public static class AgentManagementApplicationExtensions
{
    public static IServiceCollection AddAgentManagementApplication(this IServiceCollection services)
    {
        services.AddScoped<ResourceProfileService>();
        services.AddScoped<DaemonConfigService>();
        services.AddScoped<AgentService>();
        services.AddScoped<AgentGroupService>();
        services.AddScoped<PolicyAssignmentService>();
        return services;
    }
}
