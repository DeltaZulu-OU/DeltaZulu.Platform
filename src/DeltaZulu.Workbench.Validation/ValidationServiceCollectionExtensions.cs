using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DeltaZulu.Workbench.Application.Abstractions;
using DeltaZulu.Workbench.Validation.Checks;

namespace DeltaZulu.Workbench.Validation;

public static class ValidationServiceCollectionExtensions
{
    /// <summary>
    /// Registers all <see cref="ICheck"/> implementations. The check pipeline runner
    /// discovers them via <c>IEnumerable&lt;ICheck&gt;</c>.
    /// </summary>
    public static IServiceCollection AddWorkbenchValidation(this IServiceCollection services)
    {
        services.TryAddSingleton<IQuerySyntaxValidator, NonEmptyQuerySyntaxValidator>();
        services.AddSingleton<ICheck, PackageSchemaCheck>();
        services.AddSingleton<ICheck, QuerySyntaxCheck>();
        services.AddSingleton<ICheck, FixtureParseCheck>();
        services.AddSingleton<ICheck, TestDefinitionCheck>();
        services.AddSingleton<ICheck, NoteFrontmatterCheck>();
        return services;
    }
}
