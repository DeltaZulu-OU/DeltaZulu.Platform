
using DeltaZulu.Platform.Application.Analytics.Render.Directives;
using DeltaZulu.Platform.Application.Analytics.Rendering.Directives;
using DeltaZulu.Platform.Application.Analytics.Rendering.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DeltaZulu.Platform.Application.Analytics.Rendering;
public static class RenderServiceCollectionExtensions
{
    public static IServiceCollection AddAnalyticsRender(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IRenderDirectiveParser, RenderDirectiveParser>();
        services.AddSingleton<IRenderResolver, RenderResolver>();
        services.AddSingleton<IRenderChartBuilder, RenderChartBuilder>();
        return services;
    }
}