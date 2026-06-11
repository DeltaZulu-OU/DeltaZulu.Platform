using DeltaZulu.Platform.Application.Governance.Validation;
using DeltaZulu.Platform.Application.Governance.Validation.Checks;
using DeltaZulu.Platform.Domain.Governance.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace DeltaZulu.Platform.Tests.Governance.Validation;

[TestClass]
public sealed class ValidationServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddGovernanceValidation_DoesNotOverridePreRegisteredQueryValidator()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IQuerySyntaxValidator, CustomQuerySyntaxValidator>();

        services.AddGovernanceValidation();

        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IQuerySyntaxValidator>();

        Assert.IsInstanceOfType<CustomQuerySyntaxValidator>(validator);
    }

    [TestMethod]
    public void AddGovernanceValidation_RegistersDefaultQueryValidatorWhenNoneExists()
    {
        var services = new ServiceCollection();

        services.AddGovernanceValidation();

        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IQuerySyntaxValidator>();

        Assert.IsInstanceOfType<NonEmptyQuerySyntaxValidator>(validator);
    }

    private sealed class CustomQuerySyntaxValidator : IQuerySyntaxValidator
    {
        public QuerySyntaxValidationResult Validate(QuerySyntaxValidationRequest request) =>
            QuerySyntaxValidationResult.Pass();
    }
}