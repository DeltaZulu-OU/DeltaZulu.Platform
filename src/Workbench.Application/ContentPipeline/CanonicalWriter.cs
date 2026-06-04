using Workbench.Application.Abstractions;
using Workbench.Domain.Changes;
using Workbench.Domain.Enums;

namespace Workbench.Application.ContentPipeline;

/// <summary>
/// Transforms a <see cref="ChangeRequest"/>'s draft files into a <see cref="CommitRequest"/>
/// that the <see cref="IAcceptedContentStore"/> can commit atomically. This is the single
/// point where the draft-to-canonical mapping happens.
/// </summary>
/// <remarks>
/// <para>The writer is a pure function over the change request's current state. It does not
/// check merge readiness — that is the caller's responsibility. It does not touch the
/// database — the caller persists the resulting version projection.</para>
/// <para>Binary discrimination: <see cref="DraftContentType.StaticAsset"/> files are
/// committed with <see cref="ContentFile.IsBinary"/> = true; all others are text.</para>
/// </remarks>
public static class CanonicalWriter
{
    /// <summary>
    /// Produces a <see cref="CommitRequest"/> from the change request's draft files.
    /// Files present in <paramref name="existingRepoPaths"/> but absent from the draft set
    /// are included as deletes, ensuring the canonical state matches the draft exactly.
    /// </summary>
    public static CommitRequest BuildCommitRequest(
        ChangeRequest change,
        string detectionSlug,
        string authorName,
        string authorEmail,
        IReadOnlyList<string>? existingRepoPaths = null)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentException.ThrowIfNullOrWhiteSpace(detectionSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorEmail);

        var filesToWrite = new List<ContentFile>(change.DraftFiles.Count);
        var newPaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (var draft in change.DraftFiles)
        {
            var repoPath = CanonicalPathResolver.Resolve(detectionSlug, draft.Path);
            var isBinary = draft.ContentType == DraftContentType.StaticAsset;
            filesToWrite.Add(new ContentFile(repoPath, draft.Content, isBinary));
            newPaths.Add(repoPath);
        }

        // Compute deletes: files in the existing set that are not in the draft set.
        var pathsToDelete = new List<string>();
        if (existingRepoPaths is not null)
        {
            foreach (var existing in existingRepoPaths)
            {
                if (!newPaths.Contains(existing))
                {
                    pathsToDelete.Add(existing);
                }
            }
        }

        var message = $"[{change.Key}] {change.Title}";

        return new CommitRequest(
            message: message,
            authorName: authorName,
            authorEmail: authorEmail,
            filesToWrite: filesToWrite,
            pathsToDelete: pathsToDelete);
    }
}