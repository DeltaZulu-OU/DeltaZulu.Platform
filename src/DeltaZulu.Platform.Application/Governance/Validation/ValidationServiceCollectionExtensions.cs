using DeltaZulu.Platform.Application.Governance.Validation.Checks;
using DeltaZulu.Platform.Domain.Governance.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DeltaZulu.Platform.Application.Governance.Validation;

public static class ValidationServiceCollectionExtensions
{
    /// <summary>
    /// Registers all <see cref="ICheck"/> implementations. The check pipeline runner
    /// discovers them via <c>IEnumerable&lt;ICheck&gt;</c>.
    /// </summary>
    public static IServiceCollection AddGovernanceValidation(this IServiceCollection services)
    {
        services.TryAddSingleton<IQuerySyntaxValidator, NonEmptyQuerySyntaxValidator>();
        services.AddSingleton<ICheck, PackageSchemaCheck>();
        services.AddSingleton<ICheck, QuerySyntaxCheck>();
        services.AddSingleton<ICheck, FixtureParseCheck>();
        services.AddSingleton<ICheck, TestDefinitionCheck>();
        services.AddSingleton<ICheck, NoteFrontmatterCheck>();
        services.AddSingleton<ICheck, QueryExecutionDryRunCheck>();
        return services;
    }
}