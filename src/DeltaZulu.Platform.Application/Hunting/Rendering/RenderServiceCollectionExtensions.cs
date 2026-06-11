
using DeltaZulu.Platform.Application.Hunting.Render.Directives;
using DeltaZulu.Platform.Application.Hunting.Rendering.Directives;
using DeltaZulu.Platform.Application.Hunting.Rendering.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DeltaZulu.Platform.Application.Hunting.Rendering;
public static class RenderServiceCollectionExtensions
{
    public static IServiceCollection AddHuntingRender(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IRenderDirectiveParser, RenderDirectiveParser>();
        services.AddSingleton<IRenderResolver, RenderResolver>();
        services.AddSingleton<IRenderChartBuilder, RenderChartBuilder>();
        return services;
    }
}