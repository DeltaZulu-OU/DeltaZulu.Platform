using DeltaZulu.Platform.Application.AgentManagement.Validation;
using DeltaZulu.Platform.Application.AgentManagement.Validation.Checks;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace DeltaZulu.Platform.Application.AgentManagement;

public static class AgentManagementValidationExtensions
{
    public static IServiceCollection AddAgentManagementValidation(this IServiceCollection services)
    {
        services.AddSingleton<IProfileValidationCheck, ProfileSchemaCheck>();
        services.AddSingleton<IProfileValidationCheck, ResourceDescriptorCheck>();
        services.AddSingleton<IProfileValidationCheck, InputContractCheck>();
        services.AddSingleton<IProfileValidationCheck, OutputContractCheck>();
        services.AddScoped<ProfileValidationPipelineRunner>();
        return services;
    }
}
