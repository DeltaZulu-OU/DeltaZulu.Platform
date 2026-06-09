namespace Hunting.Web.Rendering;

using Hunting.Render.DependencyInjection;
using Hunting.Web.Services;
using Microsoft.Extensions.DependencyInjection;

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
