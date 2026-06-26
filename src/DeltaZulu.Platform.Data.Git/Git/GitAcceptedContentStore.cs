using System.Text;
using DeltaZulu.Platform.Domain.Governance.Contracts;
using LibGit2Sharp;
using Microsoft.Extensions.Options;

namespace DeltaZulu.Platform.Data.Git;

/// <summary>
/// LibGit2Sharp-backed implementation of <see cref="IAcceptedContentStore"/> for accepted
/// canonical detection content.
/// </summary>
public sealed class GitAcceptedContentStore : IAcceptedContentStore
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly Lock _lock = new();
    private readonly string _repositoryPath;

    /// <summary>Creates a store using configured Git repository options.</summary>
    public GitAcceptedContentStore(IOptions<GitAcceptedContentStoreOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Value.RepositoryPath);
        _repositoryPath = Path.GetFullPath(options.Value.RepositoryPath);
    }

    /// <inheritdoc />
    public Task<ContentFile?> GetFileAsync(string repositoryPath, CancellationToken ct = default)
    {
        var normalizedPath = NormalizeRepositoryPath(repositoryPath);
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            using var repository = OpenOrCreateRepository();
            var commit = repository.Head.Tip;
            return Task.FromResult(commit is null ? null : ReadFile(commit.Tree, normalizedPath));
        }
    }

    /// <inheritdoc />
    public Task<ContentFile?> GetFileAtCommitAsync(string repositoryPath, string commitSha, CancellationToken ct = default)
    {
        var normalizedPath = NormalizeRepositoryPath(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            using var repository = OpenOrCreateRepository();
            var commit = repository.Lookup<Commit>(commitSha);
            return Task.FromResult(commit is null ? null : ReadFile(commit.Tree, normalizedPath));
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ContentFile>> ListFilesAsync(string directoryPrefix, CancellationToken ct = default)
    {
        var normalizedPrefix = NormalizeDirectoryPrefix(directoryPrefix);
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            using var repository = OpenOrCreateRepository();
            var commit = repository.Head.Tip;
            if (commit is null)
            {
                return Task.FromResult<IReadOnlyList<ContentFile>>([]);
            }

            var files = new List<ContentFile>();
            CollectFiles(commit.Tree, normalizedPrefix, files, currentPrefix: string.Empty);
            return Task.FromResult<IReadOnlyList<ContentFile>>(files);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ContentFile>> ListFilesAtCommitAsync(
        string directoryPrefix, string commitSha, CancellationToken ct = default)
    {
        var normalizedPrefix = NormalizeDirectoryPrefix(directoryPrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            using var repository = OpenOrCreateRepository();
            var commit = repository.Lookup<Commit>(commitSha);
            if (commit is null)
            {
                return Task.FromResult<IReadOnlyList<ContentFile>>([]);
            }

            var files = new List<ContentFile>();
            CollectFiles(commit.Tree, normalizedPrefix, files, currentPrefix: string.Empty);
            return Task.FromResult<IReadOnlyList<ContentFile>>(files);
        }
    }

    /// <inheritdoc />
    public Task<bool> CommitExistsAsync(string commitSha, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            using var repository = OpenOrCreateRepository();
            return Task.FromResult(repository.Lookup<Commit>(commitSha) is not null);
        }
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string repositoryPath, CancellationToken ct = default)
    {
        var normalizedPath = NormalizeRepositoryPath(repositoryPath);
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            using var repository = OpenOrCreateRepository();
            var commit = repository.Head.Tip;
            return Task.FromResult(commit is not null && ReadFile(commit.Tree, normalizedPath) is not null);
        }
    }

    /// <inheritdoc />
    public Task<CommitResult> CommitAsync(CommitRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var filesToWrite = request.FilesToWrite
            .Select(file => new ContentFile(NormalizeRepositoryPath(file.RepositoryPath), file.Content, file.IsBinary))
            .ToArray();
        var pathsToDelete = request.PathsToDelete
            .Select(NormalizeRepositoryPath)
            .ToArray();

        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            using var repository = OpenOrCreateRepository();

            foreach (var file in filesToWrite)
            {
                var absolutePath = ToAbsolutePath(file.RepositoryPath);
                var parent = Path.GetDirectoryName(absolutePath);
                if (!string.IsNullOrEmpty(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                if (file.IsBinary)
                {
                    File.WriteAllBytes(absolutePath, Convert.FromBase64String(file.Content));
                }
                else
                {
                    File.WriteAllText(absolutePath, file.Content, StrictUtf8);
                }
            }

            foreach (var path in pathsToDelete)
            {
                var absolutePath = ToAbsolutePath(path);
                if (File.Exists(absolutePath))
                {
                    File.Delete(absolutePath);
                }
            }

            var stagePaths = filesToWrite
                .Select(file => file.RepositoryPath)
                .Concat(pathsToDelete)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            Commands.Stage(repository, stagePaths);

            if (!repository.RetrieveStatus().IsDirty)
            {
                var existingHead = repository.Head.Tip?.Sha;
                return !string.IsNullOrWhiteSpace(existingHead)
                    ? Task.FromResult(new CommitResult(existingHead, DateTimeOffset.UtcNow))
                    : throw new InvalidOperationException("Cannot create an accepted-content commit with no file changes.");
            }

            var committedAt = DateTimeOffset.UtcNow;
            var author = new Signature(request.AuthorName, request.AuthorEmail, committedAt);
            var commit = repository.Commit(request.Message, author, author);
            return Task.FromResult(new CommitResult(commit.Sha, committedAt));
        }
    }

    /// <inheritdoc />
    public Task<string?> GetHeadCommitShaAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            using var repository = OpenOrCreateRepository();
            return Task.FromResult(repository.Head.Tip?.Sha);
        }
    }

    private Repository OpenOrCreateRepository()
    {
        Directory.CreateDirectory(_repositoryPath);
        if (!Repository.IsValid(_repositoryPath))
        {
            Repository.Init(_repositoryPath);
        }

        return new Repository(_repositoryPath);
    }

    private string ToAbsolutePath(string normalizedRepositoryPath)
    {
        var absolutePath = Path.GetFullPath(Path.Combine(_repositoryPath, normalizedRepositoryPath.Replace('/', Path.DirectorySeparatorChar)));
        return !absolutePath.StartsWith(_repositoryPath + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(absolutePath, _repositoryPath, StringComparison.Ordinal)
            ? throw new InvalidOperationException("Resolved repository path escaped the accepted content repository.")
            : absolutePath;
    }

    private static ContentFile? ReadFile(Tree tree, string normalizedPath)
    {
        var entry = tree[normalizedPath];
        return entry?.TargetType is TreeEntryTargetType.Blob && entry.Target is Blob blob
            ? ReadBlob(normalizedPath, blob)
            : null;
    }

    private static ContentFile ReadBlob(string normalizedPath, Blob blob)
    {
        using var stream = blob.GetContentStream();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var bytes = memory.ToArray();

        try
        {
            return new ContentFile(normalizedPath, StrictUtf8.GetString(bytes));
        }
        catch (DecoderFallbackException)
        {
            return new ContentFile(normalizedPath, Convert.ToBase64String(bytes), isBinary: true);
        }
    }

    private static void CollectFiles(Tree tree, string normalizedPrefix, ICollection<ContentFile> files, string currentPrefix)
    {
        foreach (var entry in tree)
        {
            var entryPath = string.IsNullOrEmpty(currentPrefix) ? entry.Name : currentPrefix + "/" + entry.Name;
            if (entry.TargetType is TreeEntryTargetType.Blob && entry.Target is Blob blob)
            {
                if (entryPath.StartsWith(normalizedPrefix, StringComparison.Ordinal))
                {
                    files.Add(ReadBlob(entryPath, blob));
                }
            }
            else if (entry.TargetType is TreeEntryTargetType.Tree && entry.Target is Tree childTree)
            {
                CollectFiles(childTree, normalizedPrefix, files, entryPath);
            }
        }
    }

    private static string NormalizeDirectoryPrefix(string directoryPrefix)
    {
        var normalized = NormalizeRepositoryPath(directoryPrefix).TrimEnd('/');
        return normalized.Length == 0 ? string.Empty : normalized + "/";
    }

    private static string NormalizeRepositoryPath(string repositoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        var normalized = repositoryPath.Replace('\\', '/').Trim('/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 0
            || Path.IsPathRooted(repositoryPath)
            || segments.Any(segment => segment is "." or "..")
            ? throw new ArgumentException("Repository paths must be relative paths within the accepted content repository.", nameof(repositoryPath))
            : string.Join('/', segments);
    }
}