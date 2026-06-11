namespace DeltaZulu.Platform.Application.Workbench.Abstractions;

/// <summary>
/// A file read from the accepted content store (Git). Carries the repository-relative path,
/// the content (text or base64-encoded binary), and a flag indicating encoding.
/// </summary>
public sealed record ContentFile
{
    /// <summary>Repository-relative path, e.g. <c>detections/anomalous-sign-in/rule.kql</c>.</summary>
    public string RepositoryPath { get; }

    /// <summary>
    /// File content. For text files, this is the raw UTF-8 string. For binary files
    /// (<see cref="IsBinary"/> = true), this is the base64-encoded representation.
    /// </summary>
    public string Content { get; }

    /// <summary>True if the content is base64-encoded binary.</summary>
    public bool IsBinary { get; }

    public ContentFile(string repositoryPath, string content, bool isBinary = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(content);
        RepositoryPath = repositoryPath;
        Content = content;
        IsBinary = isBinary;
    }
}