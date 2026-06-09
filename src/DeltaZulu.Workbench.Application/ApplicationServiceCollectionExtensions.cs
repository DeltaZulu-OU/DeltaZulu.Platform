using Microsoft.Extensions.DependencyInjection;
using DeltaZulu.Workbench.Application.ContentLibrary;
using DeltaZulu.Workbench.Application.Services;

namespace DeltaZulu.Workbench.Application;

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
        services.AddScoped<AcceptedContentReadService>();
        services.AddScoped<MergeReconciliationService>();
        services.AddScoped<CheckPipelineRunner>();
        services.AddScoped<HuntingSavedQueryImporter>();
        return services;
    }
}
