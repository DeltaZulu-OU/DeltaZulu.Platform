using Microsoft.Extensions.DependencyInjection;
using Workbench.Application.Abstractions;

namespace Workbench.HuntingCoreAdapter;

/// <summary>Dependency-injection helpers for wiring the Hunting.Core-backed Workbench validator.</summary>
public static class HuntingCoreAdapterServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="HuntingCoreQuerySyntaxValidator"/> as Workbench's query syntax validator.
    /// The caller must also register an <see cref="IHuntingCoreQueryParser"/> implementation supplied by Hunting.Core.
    /// </summary>
    public static IServiceCollection AddHuntingCoreQueryValidation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IQuerySyntaxValidator, HuntingCoreQuerySyntaxValidator>();
        return services;
    }
}
