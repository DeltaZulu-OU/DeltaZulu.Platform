using DeltaZulu.Platform.Data.Proton.Streaming;
using DeltaZulu.Platform.Domain.Analytics.Detection;
using DeltaZulu.Platform.Domain.Analytics.Schema;
using DeltaZulu.Platform.Domain.Analytics.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace DeltaZulu.Platform.Data.Proton;

public static class ProtonServiceCollectionExtensions
{
    /// <summary>
    /// Registers Proton compilation and deployment infrastructure:
    /// <see cref="IDetectionCompilationBackend"/> and <see cref="IDetectionDeployer"/>.
    /// Call <see cref="AddProtonStreaming"/> separately to register publishers and the subscriber.
    /// </summary>
    public static IServiceCollection AddProtonDetectionBackend(this IServiceCollection services)
    {
        // Ensure ProtonHttpClientOptions is resolvable even when AddProtonStreaming is called later.
        services.TryAddSingleton(sp =>
            sp.GetRequiredService<IOptions<ProtonHttpClientOptions>>().Value);

        services.AddSingleton<ProtonHttpExecutor>();
        services.AddSingleton<IDetectionCompilationBackend, ProtonDetectionCompilationBackend>();
        services.AddSingleton<IDetectionDeployer, ProtonDetectionDeployer>();
        services.AddSingleton<ProtonSchemaEmitter>();
        services.AddKeyedSingleton<ISchemaEmitter>("proton", (sp, _) => sp.GetRequiredService<ProtonSchemaEmitter>());
        services.AddSingleton<ProtonSchemaApplier>();
        services.AddKeyedSingleton<ISchemaApplier>("proton", (sp, _) => sp.GetRequiredService<ProtonSchemaApplier>());
        return services;
    }

    /// <summary>
    /// Registers typed Bronze publishers and the stream subscriber.
    /// <paramref name="configure"/> configures <see cref="ProtonHttpClientOptions"/> inline;
    /// alternatively register it via configuration binding before calling this method.
    /// </summary>
    public static IServiceCollection AddProtonStreaming(
        this IServiceCollection services,
        Action<ProtonHttpClientOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }

        // Make the options instance directly injectable for non-options-pattern consumers.
        services.TryAddSingleton(sp =>
            sp.GetRequiredService<IOptions<ProtonHttpClientOptions>>().Value);

        services.AddSingleton<IWindowsSysmonEventPublisher, ProtonWindowsSysmonEventPublisher>();
        services.AddSingleton<IDnsServerEventPublisher, ProtonDnsServerEventPublisher>();
        services.AddSingleton<IStreamSubscriber, ProtonStreamSubscriber>();

        return services;
    }
}