using DeltaZulu.Platform.Application.Workbench.Validation;
using DeltaZulu.Platform.Application.Workbench.Validation.Checks;
using DeltaZulu.Platform.Domain.Workbench.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace DeltaZulu.Platform.Tests.Workbench.Validation;

[TestClass]
public sealed class ValidationServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddWorkbenchValidation_DoesNotOverridePreRegisteredQueryValidator()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IQuerySyntaxValidator, CustomQuerySyntaxValidator>();

        services.AddWorkbenchValidation();

        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IQuerySyntaxValidator>();

        Assert.IsInstanceOfType<CustomQuerySyntaxValidator>(validator);
    }

    [TestMethod]
    public void AddWorkbenchValidation_RegistersDefaultQueryValidatorWhenNoneExists()
    {
        var services = new ServiceCollection();

        services.AddWorkbenchValidation();

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