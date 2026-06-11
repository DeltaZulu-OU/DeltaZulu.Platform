namespace DeltaZulu.Platform.Application.Hunting.Render.DependencyInjection;

using DeltaZulu.Platform.Application.Hunting.Render.Directives;
using DeltaZulu.Platform.Application.Hunting.Render.Services;
using Microsoft.Extensions.DependencyInjection;

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