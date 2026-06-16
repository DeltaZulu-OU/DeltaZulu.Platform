namespace DeltaZulu.Platform.Domain.Governance.Contracts;

#pragma warning disable CS1574 // XML comment has cref attribute that could not be resolved

/// <summary>
/// Abstraction over the Git-backed accepted content store. The application layer talks to
/// this interface; <c>Governance.Infrastructure</c> implements it with LibGit2Sharp.
/// Tests use <see cref="Platform.Tests.Governance.Infrastructure.InMemoryContentStore"/> (in the test project) for isolation.
/// </summary>
/// <remarks>
/// <para>Per ADR-0002, the accepted content store is the authoritative source for detection
/// content after merge. The database stores operational state (drafts, checks, reviews,
/// versions); Git stores the canonical files.</para>
/// <para>Per ADR-0003, no Git concept (branch, checkout, tree, ref) leaks through this
/// interface. Callers see files, commits, and paths.</para>
/// </remarks>
public interface IAcceptedContentStore
#pragma warning restore CS1574 // XML comment has cref attribute that could not be resolved
{
    /// <summary>
    /// Reads a single file from the current HEAD of the accepted content branch.
    /// Returns <c>null</c> if the path does not exist.
    /// </summary>
    Task<ContentFile?> GetFileAsync(string repositoryPath, CancellationToken ct = default);

    /// <summary>
    /// Reads a single file at a specific commit.
    /// Returns <c>null</c> if the path does not exist at that commit.
    /// </summary>
    Task<ContentFile?> GetFileAtCommitAsync(string repositoryPath, string commitSha, CancellationToken ct = default);

    /// <summary>
    /// Lists all files under the given repository-relative directory prefix at HEAD.
    /// Returns an empty list if the directory does not exist.
    /// </summary>
    Task<IReadOnlyList<ContentFile>> ListFilesAsync(string directoryPrefix, CancellationToken ct = default);

    /// <summary>
    /// Lists all files under the given repository-relative directory prefix at a specific commit.
    /// Returns an empty list if the directory does not exist. Call <see cref="CommitExistsAsync" />
    /// first when callers must distinguish a missing commit from an empty directory.
    /// </summary>
    Task<IReadOnlyList<ContentFile>> ListFilesAtCommitAsync(
        string directoryPrefix, string commitSha, CancellationToken ct = default);

    /// <summary>
    /// Returns true if the accepted-content commit exists in the backing store.
    /// </summary>
    Task<bool> CommitExistsAsync(string commitSha, CancellationToken ct = default);

    /// <summary>
    /// Returns true if the given path exists at HEAD.
    /// </summary>
    Task<bool> ExistsAsync(string repositoryPath, CancellationToken ct = default);

    /// <summary>
    /// Atomically commits a batch of file writes and deletes to the accepted content branch.
    /// Returns the resulting commit SHA and timestamp.
    /// </summary>
    /// <exception cref="InvalidOperationException">If the commit fails (conflict, I/O error).</exception>
    Task<CommitResult> CommitAsync(CommitRequest request, CancellationToken ct = default);

    /// <summary>
    /// Returns the SHA of the current HEAD commit, or <c>null</c> if the repository is empty
    /// (no commits yet).
    /// </summary>
    Task<string?> GetHeadCommitShaAsync(CancellationToken ct = default);
}