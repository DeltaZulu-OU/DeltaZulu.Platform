using DeltaZulu.Platform.Domain.Analytics.Detection;
using Microsoft.Extensions.DependencyInjection;

namespace DeltaZulu.Platform.Data.Proton;

public static class ProtonServiceCollectionExtensions
{
    /// <summary>
    /// Registers Proton infrastructure services. Currently provides the
    /// <see cref="IDetectionCompilationBackend"/> implementation used by
    /// Application-layer detection compilers.
    /// </summary>
    public static IServiceCollection AddProtonDetectionBackend(this IServiceCollection services)
    {
        services.AddSingleton<IDetectionCompilationBackend, ProtonDetectionCompilationBackend>();
        return services;
    }
}
