using Workbench.Application.Abstractions;

namespace Workbench.Web;

/// <summary>
/// Placeholder in-memory content store for the Web host. Thread-safe for Blazor Server
/// concurrent dispatch. Replace with the LibGit2Sharp implementation in
/// <c>Workbench.Infrastructure</c> when Git integration is wired.
/// </summary>
internal sealed class PlaceholderContentStore : IAcceptedContentStore
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, ContentFile> _files = new(StringComparer.Ordinal);
    private int _commitCount;
    private string? _headSha;

    public Task<ContentFile?> GetFileAsync(string repositoryPath, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(
                _files.TryGetValue(repositoryPath, out var f) ? f : null);
        }
    }

    public Task<ContentFile?> GetFileAtCommitAsync(string repositoryPath, string commitSha, CancellationToken ct = default)
        => GetFileAsync(repositoryPath, ct);

    public Task<IReadOnlyList<ContentFile>> ListFilesAsync(string directoryPrefix, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var prefix = directoryPrefix.EndsWith('/') ? directoryPrefix : directoryPrefix + "/";
            var result = _files
                .Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal))
                .Select(kv => kv.Value)
                .ToList();
            return Task.FromResult<IReadOnlyList<ContentFile>>(result);
        }
    }

    public Task<bool> ExistsAsync(string repositoryPath, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_files.ContainsKey(repositoryPath));
        }
    }

    public Task<CommitResult> CommitAsync(CommitRequest request, CancellationToken ct = default)
    {
        lock (_lock)
        {
            foreach (var f in request.FilesToWrite) _files[f.RepositoryPath] = f;
            foreach (var p in request.PathsToDelete) _files.Remove(p);
            _headSha = $"placeholder-{++_commitCount:D6}";
            return Task.FromResult(new CommitResult(_headSha, DateTimeOffset.UtcNow));
        }
    }

    public Task<string?> GetHeadCommitShaAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_headSha);
        }
    }
}