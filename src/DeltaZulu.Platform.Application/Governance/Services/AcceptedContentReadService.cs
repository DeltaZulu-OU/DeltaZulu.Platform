using DeltaZulu.Platform.Application.Governance.ContentPipeline;
using DeltaZulu.Platform.Domain.Governance.Common;
using DeltaZulu.Platform.Domain.Governance.Contracts;
using DeltaZulu.Platform.Domain.Governance.Identifiers;

namespace DeltaZulu.Platform.Application.Governance.Services;

/// <summary>Comparison status between one draft file and current accepted content.</summary>
public enum DraftAcceptedComparisonStatus
{
    New,
    Modified,
    Unchanged,
}

/// <summary>
/// Application-level read facade for accepted canonical content. UI callers use these
/// domain read models instead of calling <see cref="IAcceptedContentStore"/> or resolving
/// repository paths directly.
/// </summary>
public sealed class AcceptedContentReadService(
    IDetectionRepository detections,
    IChangeRequestRepository changes,
    IAcceptedContentStore contentStore)
{
    /// <summary>
    /// Compares a change's draft files with the current accepted content for the target detection.
    /// This keeps canonical path calculation and content-store access inside the application layer.
    /// </summary>
    public async Task<IReadOnlyList<DraftAcceptedComparison>> CompareDraftToAcceptedAsync(
        ChangeRequestId changeId,
        CancellationToken ct = default)
    {
        var change = await changes.GetByIdAsync(changeId, ct)
            ?? throw new DomainException("change.not_found", $"Change '{changeId}' not found.");

        var detection = await detections.GetByIdAsync(change.DetectionId, ct)
            ?? throw new DomainException("detection.not_found", $"Detection '{change.DetectionId}' not found.");

        var prefix = CanonicalPathResolver.DetectionPrefix(detection.Slug);
        var acceptedFiles = await contentStore.ListFilesAsync("detections", ct);
        var acceptedByLogical = ToLogicalPathMap(prefix, acceptedFiles);

        return change.DraftFiles
            .OrderBy(file => file.Path.Value, StringComparer.Ordinal)
            .Select(draft => {
                var logicalPath = draft.Path.Value;
                acceptedByLogical.TryGetValue(logicalPath, out var accepted);

                var status = accepted is null
                    ? DraftAcceptedComparisonStatus.New
                    : draft.Content == accepted.Content
                        ? DraftAcceptedComparisonStatus.Unchanged
                        : DraftAcceptedComparisonStatus.Modified;

                return new DraftAcceptedComparison(logicalPath, status, accepted?.Content, draft.Content);
            })
            .ToList();
    }

    /// <summary>Lists accepted canonical files for a detection using UI-safe read models.</summary>
    public async Task<IReadOnlyList<AcceptedContentFileSummary>> ListAcceptedFilesForDetectionAsync(
        DetectionId detectionId,
        CancellationToken ct = default)
    {
        var detection = await detections.GetByIdAsync(detectionId, ct)
            ?? throw new DomainException("detection.not_found", $"Detection '{detectionId}' not found.");

        var prefix = CanonicalPathResolver.DetectionPrefix(detection.Slug);
        var files = await contentStore.ListFilesAsync("detections", ct);

        return files
            .Select(file => new { File = file, LogicalPath = ToLogicalPath(prefix, file.RepositoryPath) })
            .Where(file => file.LogicalPath is not null)
            .OrderBy(file => file.LogicalPath == "detection.yaml" ? 0 : 1)
            .ThenBy(file => file.LogicalPath, StringComparer.Ordinal)
            .Select(file => new AcceptedContentFileSummary(
                file.File.RepositoryPath,
                file.LogicalPath!,
                file.File.IsBinary))
            .ToList();
    }

    /// <summary>Reads one accepted canonical file for a detection using the file's logical path.</summary>
    public async Task<AcceptedContentFileDetails?> GetAcceptedFileForDetectionAsync(
        DetectionId detectionId,
        string logicalPath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalPath);

        var detection = await detections.GetByIdAsync(detectionId, ct)
            ?? throw new DomainException("detection.not_found", $"Detection '{detectionId}' not found.");

        var repositoryPath = CanonicalPathResolver.Resolve(detection.Slug, LogicalPath.Parse(logicalPath));
        var file = await contentStore.GetFileAsync(repositoryPath, ct);

        return file is null
            ? null
            : new AcceptedContentFileDetails(
                file.RepositoryPath,
                logicalPath,
                file.IsBinary,
                file.Content);
    }

    private static Dictionary<string, ContentFile> ToLogicalPathMap(
        string detectionPrefix,
        IReadOnlyList<ContentFile> files)
    {
        var acceptedByLogical = new Dictionary<string, ContentFile>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            var logicalPath = ToLogicalPath(detectionPrefix, file.RepositoryPath);
            if (logicalPath is not null)
            {
                acceptedByLogical[logicalPath] = file;
            }
        }

        return acceptedByLogical;
    }

    private static string? ToLogicalPath(string detectionPrefix, string repositoryPath)
    {
        var detectionSlug = detectionPrefix["detections/".Length..];
        return CanonicalPathResolver.TryGetLogicalPath(detectionSlug, repositoryPath);
    }
}

/// <summary>UI-safe summary of an accepted canonical file.</summary>
public sealed record AcceptedContentFileSummary(
    string RepositoryPath,
    string LogicalPath,
    bool IsBinary);

/// <summary>UI-safe accepted canonical file content.</summary>
public sealed record AcceptedContentFileDetails(
    string RepositoryPath,
    string LogicalPath,
    bool IsBinary,
    string Content);

/// <summary>UI-safe draft-to-accepted comparison read model.</summary>
public sealed record DraftAcceptedComparison(
    string LogicalPath,
    DraftAcceptedComparisonStatus Status,
    string? AcceptedContent,
    string DraftContent);