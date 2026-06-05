using Workbench.Application.Abstractions;
using Workbench.Application.ContentPipeline;
using Workbench.Domain.Common;
using Workbench.Domain.Detections;
using Workbench.Domain.Identifiers;

namespace Workbench.Application.Services;

/// <summary>
/// Read-side application service for user-facing accepted content versions.
/// </summary>
public sealed class VersionService(
    IDetectionVersionRepository versions,
    IDetectionRepository detections,
    IAcceptedContentStore contentStore)
{
    public Task<DetectionVersion?> GetByIdAsync(VersionId versionId, CancellationToken ct = default)
        => versions.GetByIdAsync(versionId, ct);

    public Task<IReadOnlyList<DetectionVersion>> ListByDetectionAsync(
        DetectionId detectionId, CancellationToken ct = default)
        => versions.ListByDetectionAsync(detectionId, ct);

    /// <summary>
    /// Compares an accepted version with the version immediately before it for the same detection.
    /// If the supplied version is the first accepted version, every file is reported as added.
    /// </summary>
    public async Task<VersionComparison> CompareWithPreviousAsync(VersionId versionId, CancellationToken ct = default)
    {
        var afterVersion = await versions.GetByIdAsync(versionId, ct)
            ?? throw new DomainException("version.not_found", $"Version '{versionId}' not found.");

        var detection = await detections.GetByIdAsync(afterVersion.DetectionId, ct)
            ?? throw new DomainException("detection.not_found", $"Detection '{afterVersion.DetectionId}' not found.");

        var detectionVersions = await versions.ListByDetectionAsync(afterVersion.DetectionId, ct);
        var orderedVersions = detectionVersions
            .OrderBy(version => version.AcceptedAt)
            .ThenBy(version => version.DisplayVersion, StringComparer.Ordinal)
            .ToArray();
        var afterIndex = Array.FindIndex(orderedVersions, version => version.Id == afterVersion.Id);
        DetectionVersion? beforeVersion = afterIndex > 0 ? orderedVersions[afterIndex - 1] : null;

        return await CompareAsync(detection, beforeVersion, afterVersion, ct);
    }

    /// <summary>
    /// Compares two accepted versions for the same detection.
    /// </summary>
    public async Task<VersionComparison> CompareAsync(
        VersionId beforeVersionId,
        VersionId afterVersionId,
        CancellationToken ct = default)
    {
        var beforeVersion = await versions.GetByIdAsync(beforeVersionId, ct)
            ?? throw new DomainException("version.not_found", $"Version '{beforeVersionId}' not found.");
        var afterVersion = await versions.GetByIdAsync(afterVersionId, ct)
            ?? throw new DomainException("version.not_found", $"Version '{afterVersionId}' not found.");

        if (beforeVersion.DetectionId != afterVersion.DetectionId)
        {
            throw new DomainException("version.detection_mismatch", "Accepted versions must belong to the same detection to be compared.");
        }

        var detection = await detections.GetByIdAsync(afterVersion.DetectionId, ct)
            ?? throw new DomainException("detection.not_found", $"Detection '{afterVersion.DetectionId}' not found.");

        return await CompareAsync(detection, beforeVersion, afterVersion, ct);
    }

    private async Task<VersionComparison> CompareAsync(
        Detection detection,
        DetectionVersion? beforeVersion,
        DetectionVersion afterVersion,
        CancellationToken ct)
    {
        var prefix = CanonicalPathResolver.DetectionPrefix(detection.Slug);
        var beforeFiles = beforeVersion is null
            ? []
            : await contentStore.ListFilesAtCommitAsync(prefix, beforeVersion.GitCommitSha, ct);
        var afterFiles = await contentStore.ListFilesAtCommitAsync(prefix, afterVersion.GitCommitSha, ct);

        var beforeByPath = ToLogicalPathMap(prefix, beforeFiles);
        var afterByPath = ToLogicalPathMap(prefix, afterFiles);
        var allPaths = beforeByPath.Keys
            .Concat(afterByPath.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal);

        var diffs = allPaths
            .Select(path => CreateDiff(path, beforeByPath.GetValueOrDefault(path), afterByPath.GetValueOrDefault(path)))
            .ToArray();

        return new VersionComparison(detection, beforeVersion, afterVersion, diffs);
    }

    private static Dictionary<string, ContentFile> ToLogicalPathMap(string detectionPrefix, IReadOnlyList<ContentFile> files)
    {
        var prefix = detectionPrefix.TrimEnd('/') + "/";
        return files.ToDictionary(file => ToLogicalPath(prefix, file.RepositoryPath), StringComparer.Ordinal);
    }

    private static string ToLogicalPath(string prefix, string repositoryPath)
    {
        if (!repositoryPath.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new DomainException("version.path_outside_detection", $"Accepted content path '{repositoryPath}' is outside the detection package.");
        }

        return repositoryPath[prefix.Length..];
    }

    private static VersionFileDiff CreateDiff(string logicalPath, ContentFile? before, ContentFile? after)
    {
        var status = (before, after) switch
        {
            (null, not null) => VersionFileDiffStatus.Added,
            (not null, null) => VersionFileDiffStatus.Removed,
            (not null, not null) when before.Content != after.Content || before.IsBinary != after.IsBinary => VersionFileDiffStatus.Modified,
            _ => VersionFileDiffStatus.Unchanged,
        };

        return new VersionFileDiff(
            logicalPath,
            status,
            before?.Content,
            after?.Content,
            before?.IsBinary ?? false,
            after?.IsBinary ?? false,
            CreateHunks(status, before, after));
    }

    private static IReadOnlyList<VersionDiffHunk> CreateHunks(
        VersionFileDiffStatus status,
        ContentFile? before,
        ContentFile? after)
    {
        if (status is VersionFileDiffStatus.Unchanged || (before?.IsBinary ?? false) || (after?.IsBinary ?? false))
        {
            return [];
        }

        var beforeLines = SplitLines(before?.Content);
        var afterLines = SplitLines(after?.Content);
        if (beforeLines.Length == 0 && afterLines.Length == 0)
        {
            return [];
        }

        var lines = CreateDiffLines(beforeLines, afterLines);
        var beforeLineCount = lines.Count(line => line.BeforeLineNumber is not null);
        var afterLineCount = lines.Count(line => line.AfterLineNumber is not null);

        return
        [
            new VersionDiffHunk(
                lines.FirstOrDefault(line => line.BeforeLineNumber is not null)?.BeforeLineNumber ?? 1,
                beforeLineCount,
                lines.FirstOrDefault(line => line.AfterLineNumber is not null)?.AfterLineNumber ?? 1,
                afterLineCount,
                lines),
        ];
    }

    private static string[] SplitLines(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return [];
        }

        return content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static IReadOnlyList<VersionDiffLine> CreateDiffLines(string[] beforeLines, string[] afterLines)
    {
        var lengths = BuildLongestCommonSubsequenceLengths(beforeLines, afterLines);
        var result = new List<VersionDiffLine>();
        var beforeIndex = 0;
        var afterIndex = 0;

        while (beforeIndex < beforeLines.Length || afterIndex < afterLines.Length)
        {
            if (beforeIndex < beforeLines.Length
                && afterIndex < afterLines.Length
                && beforeLines[beforeIndex] == afterLines[afterIndex])
            {
                result.Add(new VersionDiffLine(
                    VersionDiffLineType.Context,
                    beforeIndex + 1,
                    afterIndex + 1,
                    beforeLines[beforeIndex]));
                beforeIndex++;
                afterIndex++;
            }
            else if (afterIndex < afterLines.Length
                && (beforeIndex == beforeLines.Length
                    || lengths[beforeIndex, afterIndex + 1] >= lengths[beforeIndex + 1, afterIndex]))
            {
                result.Add(new VersionDiffLine(
                    VersionDiffLineType.Added,
                    null,
                    afterIndex + 1,
                    afterLines[afterIndex]));
                afterIndex++;
            }
            else
            {
                result.Add(new VersionDiffLine(
                    VersionDiffLineType.Removed,
                    beforeIndex + 1,
                    null,
                    beforeLines[beforeIndex]));
                beforeIndex++;
            }
        }

        return result;
    }

    private static int[,] BuildLongestCommonSubsequenceLengths(string[] beforeLines, string[] afterLines)
    {
        var lengths = new int[beforeLines.Length + 1, afterLines.Length + 1];
        for (var beforeIndex = beforeLines.Length - 1; beforeIndex >= 0; beforeIndex--)
        {
            for (var afterIndex = afterLines.Length - 1; afterIndex >= 0; afterIndex--)
            {
                lengths[beforeIndex, afterIndex] = beforeLines[beforeIndex] == afterLines[afterIndex]
                    ? lengths[beforeIndex + 1, afterIndex + 1] + 1
                    : Math.Max(lengths[beforeIndex + 1, afterIndex], lengths[beforeIndex, afterIndex + 1]);
            }
        }

        return lengths;
    }
}
