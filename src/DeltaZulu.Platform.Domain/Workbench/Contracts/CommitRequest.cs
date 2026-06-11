namespace DeltaZulu.Platform.Application.Workbench.Abstractions;

/// <summary>
/// A batch of files to commit atomically to the accepted content store. Produced by the
/// canonical writer; consumed by <see cref="IAcceptedContentStore.CommitAsync"/>.
/// </summary>
public sealed record CommitRequest
{
    /// <summary>Human-readable commit message.</summary>
    public string Message { get; }

    /// <summary>Author name for the Git commit.</summary>
    public string AuthorName { get; }

    /// <summary>Author email for the Git commit.</summary>
    public string AuthorEmail { get; }

    /// <summary>Files to write (add or update). Paths are repository-relative.</summary>
    public IReadOnlyList<ContentFile> FilesToWrite { get; }

    /// <summary>Repository-relative paths to delete.</summary>
    public IReadOnlyList<string> PathsToDelete { get; }

    public CommitRequest(
        string message,
        string authorName,
        string authorEmail,
        IReadOnlyList<ContentFile> filesToWrite,
        IReadOnlyList<string>? pathsToDelete = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorEmail);
        ArgumentNullException.ThrowIfNull(filesToWrite);

        Message = message;
        AuthorName = authorName;
        AuthorEmail = authorEmail;
        FilesToWrite = filesToWrite;
        PathsToDelete = pathsToDelete ?? [];
    }
}