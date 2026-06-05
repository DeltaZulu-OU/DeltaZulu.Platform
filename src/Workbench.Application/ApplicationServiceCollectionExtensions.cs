using Microsoft.Extensions.DependencyInjection;
using Workbench.Application.Services;

namespace Workbench.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Registers application services. Call after persistence registration so repositories are available.</summary>
    public static IServiceCollection AddWorkbenchApplication(this IServiceCollection services)
    {
        services.AddScoped<DetectionContentService>();
        services.AddScoped<IssueService>();
        services.AddScoped<ChangeService>();
        services.AddScoped<MergeService>();
        services.AddScoped<VersionService>();
        services.AddScoped<RestoreService>();
        services.AddScoped<MergeReconciliationService>();
        services.AddScoped<CheckPipelineRunner>();
        return services;
    }
}
