using DeltaZulu.Workbench.Application.Abstractions;
using DeltaZulu.Workbench.Application.ContentPipeline;
using DeltaZulu.Workbench.Domain.Changes;
using DeltaZulu.Workbench.Domain.Common;
using DeltaZulu.Workbench.Domain.Enums;
using DeltaZulu.Workbench.Domain.Identifiers;

namespace DeltaZulu.Workbench.Application.Services;

/// <summary>
/// Application service for restoring accepted detection content by opening a new database-owned change.
/// </summary>
public sealed class RestoreService(
    IDetectionRepository detections,
    IDetectionVersionRepository versions,
    IChangeRequestRepository changes,
    IAcceptedContentStore contentStore,
    IWorkflowOrchestrator orchestrator,
    TimeProvider time)
{
    /// <summary>
    /// Opens a new change populated with the canonical files from an accepted version.
    /// The restore does not rewrite accepted history; normal workflow gates apply before merge.
    /// </summary>
    public async Task<ChangeRequest> RestoreVersionAsChangeAsync(
        VersionId versionId,
        UserId authorId,
        WorkflowProfileId workflowProfileId,
        string? key = null,
        string? title = null,
        IssueId? linkedIssueId = null,
        CancellationToken ct = default)
    {
        var version = await versions.GetByIdAsync(versionId, ct)
            ?? throw new DomainException("version.not_found", $"Version '{versionId}' not found.");

        var detection = await detections.GetByIdAsync(version.DetectionId, ct)
            ?? throw new DomainException("detection.not_found", $"Detection '{version.DetectionId}' not found.");

        var restoreKey = string.IsNullOrWhiteSpace(key)
            ? CreateRestoreKey(version.DisplayVersion)
            : key;
        var restoreTitle = string.IsNullOrWhiteSpace(title)
            ? $"Restore {version.DisplayVersion}: {version.ChangeSummary}"
            : title;

        var change = ChangeRequest.Open(
            ChangeRequestId.New(), restoreKey, restoreTitle, detection.Id, authorId,
            workflowProfileId, detection.CurrentVersionId, time.GetUtcNow(), linkedIssueId ?? version.LinkedIssueId);

        var prefix = CanonicalPathResolver.DetectionPrefix(detection.Slug);
        if (!await contentStore.CommitExistsAsync(version.GitCommitSha, ct))
        {
            throw new DomainException(
                "accepted_content.commit_missing",
                $"Accepted content for version {version.DisplayVersion} is unavailable. Run accepted-content reconciliation before comparing or restoring this version.");
        }

        var files = await contentStore.ListFilesAtCommitAsync(prefix, version.GitCommitSha, ct);
        if (files.Count == 0)
        {
            throw new DomainException("restore.content_missing",
                $"No accepted content files were found for version {version.DisplayVersion}.");
        }

        foreach (var file in files.OrderBy(f => f.RepositoryPath, StringComparer.Ordinal))
        {
            var logicalPath = ToLogicalPath(prefix, file.RepositoryPath);
            change.UpsertDraftFile(
                LogicalPath.Parse(logicalPath), InferContentType(logicalPath, file), file.Content,
                authorId, time.GetUtcNow());
        }

        changes.Add(change);
        changes.Save(change);
        await orchestrator.OnChangeOpenedAsync(change.Id, workflowProfileId, ct);
        return change;
    }

    private static string CreateRestoreKey(string displayVersion)
    {
        var safeVersion = new string(displayVersion.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrWhiteSpace(safeVersion))
        {
            safeVersion = "version";
        }

        return $"RST-{safeVersion}-{Guid.NewGuid():N}"[..32];
    }

    private static string ToLogicalPath(string detectionPrefix, string repositoryPath)
    {
        var prefix = detectionPrefix.TrimEnd('/') + "/";
        return !repositoryPath.StartsWith(prefix, StringComparison.Ordinal)
            ? throw new DomainException("restore.path_outside_detection",
                $"Accepted content path '{repositoryPath}' is outside the detection package.")
            : repositoryPath[prefix.Length..];
    }

    private static DraftContentType InferContentType(string logicalPath, ContentFile file)
    {
        return file.IsBinary
            ? DraftContentType.StaticAsset
            : logicalPath switch
        {
            "detection.yaml" => DraftContentType.DetectionMetadata,
            "rule.kql" => DraftContentType.HuntingQuery,
            _ when logicalPath.StartsWith("tests/", StringComparison.Ordinal) => DraftContentType.TestDefinition,
            _ when logicalPath.StartsWith("fixtures/", StringComparison.Ordinal) => DraftContentType.Fixture,
            _ when logicalPath.StartsWith("notes/", StringComparison.Ordinal)
                && logicalPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase) => DraftContentType.InvestigationNote,
            _ => DraftContentType.DetectionMetadata,
        };
    }
}
