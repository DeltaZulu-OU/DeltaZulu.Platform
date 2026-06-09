using Workbench.Application.Abstractions;

namespace Workbench.Tests.Infrastructure;

/// <summary>
/// In-memory implementation of <see cref="IAcceptedContentStore"/> for deterministic tests.
/// Each commit snapshots the full tree; commits are identified by a sequential fake SHA.
/// Thread-safe for concurrent test scenarios.
/// </summary>
internal sealed class InMemoryContentStore : IAcceptedContentStore
{
    private readonly Lock _lock = new();
    private int _commitCounter;

    /// <summary>Current HEAD tree: repository-relative path → content file.</summary>
    private readonly Dictionary<string, ContentFile> _head = new(StringComparer.Ordinal);

    /// <summary>Per-commit snapshots: commit SHA → (path → content file).</summary>
    private readonly Dictionary<string, Dictionary<string, ContentFile>> _commits = new(StringComparer.Ordinal);

    /// <summary>SHA of the current HEAD, or null if no commits.</summary>
    private string? _headSha;

    /// <summary>All commits in order, for inspection in tests.</summary>
    public IReadOnlyList<string> CommitLog
    {
        get
        {
            lock (_lock)
            {
                return _commits.Keys.ToList();
            }
        }
    }

    public Task<ContentFile?> GetFileAsync(string repositoryPath, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(
                _head.TryGetValue(repositoryPath, out var file) ? file : null);
        }
    }

    public Task<ContentFile?> GetFileAtCommitAsync(string repositoryPath, string commitSha, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return !_commits.TryGetValue(commitSha, out var snapshot)
                ? Task.FromResult<ContentFile?>(null)
                : Task.FromResult(
                snapshot.TryGetValue(repositoryPath, out var file) ? file : null);
        }
    }

    public Task<IReadOnlyList<ContentFile>> ListFilesAsync(string directoryPrefix, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var prefix = directoryPrefix.EndsWith('/') ? directoryPrefix : directoryPrefix + "/";
            var result = _head
                .Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal))
                .Select(kv => kv.Value)
                .ToList();
            return Task.FromResult<IReadOnlyList<ContentFile>>(result);
        }
    }

    public Task<IReadOnlyList<ContentFile>> ListFilesAtCommitAsync(
        string directoryPrefix, string commitSha, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_commits.TryGetValue(commitSha, out var snapshot))
            {
                return Task.FromResult<IReadOnlyList<ContentFile>>([]);
            }

            var prefix = directoryPrefix.EndsWith('/') ? directoryPrefix : directoryPrefix + "/";
            var result = snapshot
                .Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal))
                .Select(kv => kv.Value)
                .ToList();
            return Task.FromResult<IReadOnlyList<ContentFile>>(result);
        }
    }

    public Task<bool> CommitExistsAsync(string commitSha, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_commits.ContainsKey(commitSha));
        }
    }

    public void ForgetCommit(string commitSha)
    {
        lock (_lock)
        {
            _commits.Remove(commitSha);
            if (_headSha == commitSha)
            {
                _headSha = _commits.Keys.LastOrDefault();
            }
        }
    }

    public Task<bool> ExistsAsync(string repositoryPath, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_head.ContainsKey(repositoryPath));
        }
    }

    public Task<CommitResult> CommitAsync(CommitRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_lock)
        {
            // Apply writes.
            foreach (var file in request.FilesToWrite)
            {
                _head[file.RepositoryPath] = file;
            }

            // Apply deletes.
            foreach (var path in request.PathsToDelete)
            {
                _head.Remove(path);
            }

            // Snapshot.
            var sha = $"fake-sha-{++_commitCounter:D6}";
            var snapshot = new Dictionary<string, ContentFile>(_head, StringComparer.Ordinal);
            _commits[sha] = snapshot;
            _headSha = sha;

            var result = new CommitResult(sha, DateTimeOffset.UtcNow);
            return Task.FromResult(result);
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
