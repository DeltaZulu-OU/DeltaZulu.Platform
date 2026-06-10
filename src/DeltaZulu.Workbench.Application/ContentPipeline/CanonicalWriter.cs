using DeltaZulu.Workbench.Application.Abstractions;
using DeltaZulu.Workbench.Domain.Changes;
using DeltaZulu.Workbench.Domain.Enums;

namespace DeltaZulu.Workbench.Application.ContentPipeline;

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
    /// Draft files are patch-like: absent accepted files are preserved rather than deleted.
    /// Explicit accepted-content deletion is intentionally not modeled in the current POC.
    /// </summary>
    public static CommitRequest BuildCommitRequest(
        ChangeRequest change,
        string detectionSlug,
        string authorName,
        string authorEmail)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentException.ThrowIfNullOrWhiteSpace(detectionSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorEmail);

        var filesToWrite = new List<ContentFile>(change.DraftFiles.Count);

        foreach (var draft in change.DraftFiles)
        {
            var repoPath = CanonicalPathResolver.Resolve(detectionSlug, draft.Path);
            var isBinary = draft.ContentType == DraftContentType.StaticAsset;
            filesToWrite.Add(new ContentFile(repoPath, draft.Content, isBinary));
        }

        // Existing accepted files are intentionally preserved when they are absent from the
        // draft. A change's draft content represents the files being added or updated, not
        // a full package replacement. Until the domain models explicit deletion intent,
        // automatic delete inference would let a partial edit remove unrelated content.

        var message = $"[{change.Key}] {change.Title}";

        return new CommitRequest(
            message: message,
            authorName: authorName,
            authorEmail: authorEmail,
            filesToWrite: filesToWrite);
    }
}