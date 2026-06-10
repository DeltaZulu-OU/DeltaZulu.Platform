using DeltaZulu.DetectionContent.Paths;

namespace DeltaZulu.DetectionContent.Files;

/// <summary>Logical accepted-content file payload that is independent of any store implementation.</summary>
public sealed record DetectionContentFile
{
    /// <summary>Validated repository-relative accepted-content path.</summary>
    public DetectionRepositoryPath Path { get; }

    /// <summary>Repository-relative accepted-content path.</summary>
    public string RepositoryPath => Path.Value;

    /// <summary>Raw text content, or base64 for binary content when <see cref="IsBinary" /> is true.</summary>
    public string Content { get; }

    /// <summary>True when <see cref="Content" /> is a base64-encoded binary payload.</summary>
    public bool IsBinary { get; }

    /// <summary>Creates a logical accepted-content file payload from a repository-relative path.</summary>
    public DetectionContentFile(string repositoryPath, string content, bool isBinary = false)
        : this(DetectionRepositoryPath.Parse(repositoryPath), content, isBinary)
    {
    }

    /// <summary>Creates a logical accepted-content file payload from a validated repository path.</summary>
    public DetectionContentFile(DetectionRepositoryPath path, string content, bool isBinary = false)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(content);

        Path = path;
        Content = content;
        IsBinary = isBinary;
    }
}