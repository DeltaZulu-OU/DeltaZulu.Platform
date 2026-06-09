using Microsoft.Extensions.DependencyInjection;
using DeltaZulu.Workbench.Application.Abstractions;
using DeltaZulu.Workbench.Validation;
using DeltaZulu.Workbench.Validation.Checks;

namespace DeltaZulu.Workbench.Tests.Validation;

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
