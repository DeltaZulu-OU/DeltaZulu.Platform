using DeltaZulu.Platform.Application.Analytics.Rendering;
using DeltaZulu.Platform.Web.Analytics.Services;

namespace DeltaZulu.Platform.Web.Analytics.Rendering;

public static class AnalyticsRenderWebServiceCollectionExtensions
{
    public static IServiceCollection AddAnalyticsRenderWeb(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAnalyticsRender();
        services.AddScoped<IDataOnlyQueryService>(sp => sp.GetRequiredService<QueryService>());
        services.AddScoped<EChartsRenderOptionsBuilder>();
        services.AddScoped<RenderedQueryRunner>();
        services.AddScoped<IRenderedQueryRunner>(sp => sp.GetRequiredService<RenderedQueryRunner>());

        return services;
    }
}