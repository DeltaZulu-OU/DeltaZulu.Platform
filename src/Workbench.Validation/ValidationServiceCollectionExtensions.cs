using Microsoft.Extensions.DependencyInjection;
using Workbench.Application.Abstractions;
using Workbench.Validation.Checks;

namespace Workbench.Validation;

public static class ValidationServiceCollectionExtensions
{
    /// <summary>
    /// Registers all <see cref="ICheck"/> implementations. The check pipeline runner
    /// discovers them via <c>IEnumerable&lt;ICheck&gt;</c>.
    /// </summary>
    public static IServiceCollection AddWorkbenchValidation(this IServiceCollection services)
    {
        services.AddSingleton<ICheck, PackageSchemaCheck>();
        services.AddSingleton<ICheck, QuerySyntaxCheck>();
        services.AddSingleton<ICheck, FixtureParseCheck>();
        services.AddSingleton<ICheck, TestDefinitionCheck>();
        services.AddSingleton<ICheck, NoteFrontmatterCheck>();
        return services;
    }
}
