using Workbench.Domain.Detections;

namespace Workbench.Application.Services;

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
/// File-level comparison result that intentionally avoids exposing repository internals.
/// </summary>
public sealed record VersionFileDiff(
    string LogicalPath,
    VersionFileDiffStatus Status,
    string? BeforeContent,
    string? AfterContent,
    bool BeforeIsBinary,
    bool AfterIsBinary)
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
