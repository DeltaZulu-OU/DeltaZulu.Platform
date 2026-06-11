using DeltaZulu.Platform.Domain.Workbench.Detections;

namespace DeltaZulu.Platform.Application.Workbench.Services;

/// <summary>
/// User-facing status for a file between two accepted detection versions.
/// </summary>
public enum VersionFileDiffStatus
{
    Added,
    Modified,
    Removed,
    Unchanged,
}

/// <summary>
/// User-facing line status within a version comparison hunk.
/// </summary>
public enum VersionDiffLineType
{
    Context,
    Added,
    Removed,
}

/// <summary>
/// One user-facing line within a version comparison hunk.
/// </summary>
public sealed record VersionDiffLine(
    VersionDiffLineType Type,
    int? BeforeLineNumber,
    int? AfterLineNumber,
    string Text);

/// <summary>
/// A contiguous set of changed and surrounding lines in a file comparison.
/// </summary>
public sealed record VersionDiffHunk(
    int BeforeStartLine,
    int BeforeLineCount,
    int AfterStartLine,
    int AfterLineCount,
    IReadOnlyList<VersionDiffLine> Lines);

/// <summary>
/// File-level comparison result that intentionally avoids exposing repository internals.
/// </summary>
public sealed record VersionFileDiff(
    string LogicalPath,
    VersionFileDiffStatus Status,
    string? BeforeContent,
    string? AfterContent,
    bool BeforeIsBinary,
    bool AfterIsBinary,
    IReadOnlyList<VersionDiffHunk> Hunks)
{
    public bool IsBinary => BeforeIsBinary || AfterIsBinary;
}

/// <summary>
/// Domain-friendly comparison between two accepted detection versions.
/// </summary>
public sealed record VersionComparison(
    Detection Detection,
    DetectionVersion? BeforeVersion,
    DetectionVersion AfterVersion,
    IReadOnlyList<VersionFileDiff> Files)
{
    public IReadOnlyList<VersionFileDiff> ChangedFiles => Files
        .Where(file => file.Status is not VersionFileDiffStatus.Unchanged)
        .ToArray();
}