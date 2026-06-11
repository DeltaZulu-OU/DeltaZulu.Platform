using DeltaZulu.Platform.Application.Workbench.ContentLibrary;
using DeltaZulu.Platform.Application.Workbench.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DeltaZulu.Platform.Application.Workbench;

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