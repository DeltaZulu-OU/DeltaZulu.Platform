using DeltaZulu.Platform.Application.Governance.ContentLibrary;
using DeltaZulu.Platform.Application.Governance.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DeltaZulu.Platform.Application.Governance;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Registers application services. Call after persistence registration so repositories are available.</summary>
    public static IServiceCollection AddGovernanceApplication(this IServiceCollection services)
    {
        services.AddScoped<DetectionContentService>();
        services.AddScoped<IssueService>();
        services.AddScoped<ChangeService>();
        services.AddScoped<MergeService>();
        services.AddScoped<IDetectionProjectionService, DetectionProjectionService>();
        services.AddScoped<VersionService>();
        services.AddScoped<RestoreService>();
        services.AddScoped<AcceptedContentReadService>();
        services.AddScoped<MergeReconciliationService>();
        services.AddScoped<CheckPipelineRunner>();
        services.AddScoped<SavedQueryImporter>();
        return services;
    }
}