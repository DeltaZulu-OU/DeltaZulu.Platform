using DeltaZulu.Workbench.Application.Abstractions;
using DeltaZulu.Workbench.Infrastructure.AcceptedContent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DeltaZulu.Workbench.Infrastructure;

/// <summary>Dependency injection registration for infrastructure adapters.</summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Registers the Git-backed accepted content store.</summary>
    public static IServiceCollection AddWorkbenchGitAcceptedContentStore(
        this IServiceCollection services,
        Action<GitAcceptedContentStoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddSingleton<IValidateOptions<GitAcceptedContentStoreOptions>, GitAcceptedContentStoreOptionsValidator>();
        services.AddSingleton<IAcceptedContentStore, GitAcceptedContentStore>();
        return services;
    }

    private sealed class GitAcceptedContentStoreOptionsValidator : IValidateOptions<GitAcceptedContentStoreOptions>
    {
        public ValidateOptionsResult Validate(string? name, GitAcceptedContentStoreOptions options)
            => string.IsNullOrWhiteSpace(options.RepositoryPath)
                ? ValidateOptionsResult.Fail("Accepted content repository path is required.")
                : ValidateOptionsResult.Success;
    }
}