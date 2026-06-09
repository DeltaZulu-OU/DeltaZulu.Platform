namespace Workbench.Infrastructure.AcceptedContent;

/// <summary>Configuration for the Git-backed accepted content store.</summary>
public sealed class GitAcceptedContentStoreOptions
{
    /// <summary>
    /// Filesystem path to the local Git repository that stores accepted canonical content.
    /// Relative paths are resolved against the application content root by the web host.
    /// </summary>
    public string RepositoryPath { get; set; } = "accepted-content";
}
