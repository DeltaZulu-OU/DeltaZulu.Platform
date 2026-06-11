
using DeltaZulu.Platform.Application.Hunting.Rendering;
using DeltaZulu.Platform.Web.Hunting.Services;

namespace DeltaZulu.Platform.Web.Hunting.Rendering;
public static class HuntingRenderWebServiceCollectionExtensions
{
    public static IServiceCollection AddHuntingRenderWeb(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHuntingRender();
        services.AddScoped<IDataOnlyQueryService>(sp => sp.GetRequiredService<QueryService>());
        services.AddScoped<EChartsRenderOptionsBuilder>();
        services.AddScoped<RenderedQueryRunner>();
        services.AddScoped<IRenderedQueryRunner>(sp => sp.GetRequiredService<RenderedQueryRunner>());

        return services;
    }
}