using DeltaZulu.Workbench.Application.Abstractions;
using DeltaZulu.Workbench.Application.ContentPipeline;
using DeltaZulu.Workbench.Domain.Common;
using DeltaZulu.Workbench.Domain.Detections;
using DeltaZulu.Workbench.Domain.Identifiers;

namespace DeltaZulu.Workbench.Application.Services;

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

    public Task<IReadOnlyList<DetectionVersion>> ListRecentAsync(int count, CancellationToken ct = default)
        => versions.ListRecentAsync(count, ct);

    /// <summary>
    /// Lists accepted versions that can be used as historical comparison baselines for the supplied version.
    /// </summary>
    public async Task<IReadOnlyList<DetectionVersion>> ListComparisonBaselinesAsync(
        VersionId afterVersionId,
        CancellationToken ct = default)
    {
        var afterVersion = await versions.GetByIdAsync(afterVersionId, ct)
            ?? throw new DomainException("version.not_found", $"Version '{afterVersionId}' not found.");

        var detectionVersions = await versions.ListByDetectionAsync(afterVersion.DetectionId, ct);
        return detectionVersions
            .Where(version => IsBefore(version, afterVersion))
            .OrderByDescending(version => version.AcceptedAt)
            .ThenByDescending(version => version.SequenceNumber)
            .ThenByDescending(version => version.DisplayVersion, StringComparer.Ordinal)
            .ToArray();
    }

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

        if (!IsBefore(beforeVersion, afterVersion))
        {
            throw new DomainException("version.baseline_not_before", "Comparison baseline must be an earlier accepted version for the same detection.");
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
        if (beforeVersion is not null)
        {
            await EnsureVersionCommitExistsAsync(beforeVersion, ct);
        }

        await EnsureVersionCommitExistsAsync(afterVersion, ct);

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

    private async Task EnsureVersionCommitExistsAsync(DetectionVersion version, CancellationToken ct)
    {
        if (await contentStore.CommitExistsAsync(version.GitCommitSha, ct))
        {
            return;
        }

        throw new DomainException(
            "accepted_content.commit_missing",
            $"Accepted content for version {version.DisplayVersion} is unavailable. Run accepted-content reconciliation before comparing or restoring this version.");
    }

    private static Dictionary<string, ContentFile> ToLogicalPathMap(string detectionPrefix, IReadOnlyList<ContentFile> files)
    {
        var prefix = detectionPrefix.TrimEnd('/') + "/";
        return files.ToDictionary(file => ToLogicalPath(prefix, file.RepositoryPath), StringComparer.Ordinal);
    }

    private static bool IsBefore(DetectionVersion candidate, DetectionVersion afterVersion)
    {
        return candidate.Id == afterVersion.Id
            ? false
            : candidate.AcceptedAt < afterVersion.AcceptedAt
            || (candidate.AcceptedAt == afterVersion.AcceptedAt && candidate.SequenceNumber < afterVersion.SequenceNumber);
    }

    private static string ToLogicalPath(string prefix, string repositoryPath)
    {
        return !repositoryPath.StartsWith(prefix, StringComparison.Ordinal)
            ? throw new DomainException("version.path_outside_detection", $"Accepted content path '{repositoryPath}' is outside the detection package.")
            : repositoryPath[prefix.Length..];
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
        return CreateDiffHunks(lines);
    }

    private static IReadOnlyList<VersionDiffHunk> CreateDiffHunks(IReadOnlyList<VersionDiffLine> lines)
    {
        const int contextLineCount = 3;

        var changedIndexes = lines
            .Select((line, index) => new { line, index })
            .Where(item => item.line.Type is not VersionDiffLineType.Context)
            .Select(item => item.index)
            .ToArray();

        if (changedIndexes.Length == 0)
        {
            return [];
        }

        var ranges = new List<(int Start, int End)>();
        foreach (var changedIndex in changedIndexes)
        {
            var start = Math.Max(0, changedIndex - contextLineCount);
            var end = Math.Min(lines.Count - 1, changedIndex + contextLineCount);

            if (ranges.Count > 0 && start <= ranges[^1].End + 1)
            {
                ranges[^1] = (ranges[^1].Start, Math.Max(ranges[^1].End, end));
            }
            else
            {
                ranges.Add((start, end));
            }
        }

        return ranges
            .Select(range => CreateHunk(lines.Skip(range.Start).Take(range.End - range.Start + 1).ToArray()))
            .ToArray();
    }

    private static VersionDiffHunk CreateHunk(IReadOnlyList<VersionDiffLine> lines)
    {
        var beforeLineCount = lines.Count(line => line.BeforeLineNumber is not null);
        var afterLineCount = lines.Count(line => line.AfterLineNumber is not null);

        return new VersionDiffHunk(
            lines.FirstOrDefault(line => line.BeforeLineNumber is not null)?.BeforeLineNumber ?? 1,
            beforeLineCount,
            lines.FirstOrDefault(line => line.AfterLineNumber is not null)?.AfterLineNumber ?? 1,
            afterLineCount,
            lines);
    }

    private static string[] SplitLines(string? content)
    {
        return string.IsNullOrEmpty(content)
            ? []
            : content
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