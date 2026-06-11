using DeltaZulu.Platform.Application.Workbench.ContentPipeline;
using DeltaZulu.Platform.Domain.Workbench.Common;
using DeltaZulu.Platform.Domain.Workbench.Contracts;
using DeltaZulu.Platform.Domain.Workbench.Identifiers;

namespace DeltaZulu.Platform.Application.Workbench.Services;

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
        var acceptedFiles = await contentStore.ListFilesAsync(prefix, ct);
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
        var files = await contentStore.ListFilesAsync(prefix, ct);
        var prefixWithSlash = prefix.TrimEnd('/') + "/";

        return files
            .Where(file => file.RepositoryPath.StartsWith(prefixWithSlash, StringComparison.Ordinal))
            .OrderBy(file => file.RepositoryPath, StringComparer.Ordinal)
            .Select(file => new AcceptedContentFileSummary(
                file.RepositoryPath,
                file.RepositoryPath[prefixWithSlash.Length..],
                file.IsBinary))
            .ToList();
    }

    private static Dictionary<string, ContentFile> ToLogicalPathMap(
        string detectionPrefix,
        IReadOnlyList<ContentFile> files)
    {
        var prefixWithSlash = detectionPrefix.TrimEnd('/') + "/";
        var acceptedByLogical = new Dictionary<string, ContentFile>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            if (file.RepositoryPath.StartsWith(prefixWithSlash, StringComparison.Ordinal))
            {
                acceptedByLogical[file.RepositoryPath[prefixWithSlash.Length..]] = file;
            }
        }

        return acceptedByLogical;
    }
}

/// <summary>UI-safe summary of an accepted canonical file.</summary>
public sealed record AcceptedContentFileSummary(
    string RepositoryPath,
    string LogicalPath,
    bool IsBinary);
/// <summary>UI-safe draft-to-accepted comparison read model.</summary>
public sealed record DraftAcceptedComparison(
    string LogicalPath,
    DraftAcceptedComparisonStatus Status,
    string? AcceptedContent,
    string DraftContent);