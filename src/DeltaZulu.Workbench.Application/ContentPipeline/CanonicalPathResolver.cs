using DeltaZulu.DetectionContent.Paths;
using Workbench.Domain.Identifiers;

namespace Workbench.Application.ContentPipeline;

/// <summary>
/// Workbench compatibility adapter for the shared detection-content accepted path convention.
/// Shared products should prefer <see cref="DetectionContentPathResolver" /> directly once
/// Workbench identifiers are translated to shared detection-content value objects.
/// </summary>
/// <remarks>
/// <para>Repository layout:</para>
/// <code>
/// detections/&lt;slug&gt;/detection.yaml
/// detections/&lt;slug&gt;/rule.kql
/// detections/&lt;slug&gt;/tests/baseline.yaml
/// detections/&lt;slug&gt;/notes/investigation.md
/// detections/&lt;slug&gt;/notes/assets/timeline.png
/// </code>
/// <para>The public convention belongs to <see cref="DetectionContentPathResolver" />. This
/// adapter preserves the existing Workbench API surface while delegating the convention to the
/// shared detection-content package. Workbench domain validation should already have accepted
/// the slug and logical path before application services call this adapter.</para>
/// </remarks>
public static class CanonicalPathResolver
{
    /// <summary>
    /// Resolves a logical path within a detection to a repository-relative path.
    /// </summary>
    /// <param name="detectionSlug">The detection's slug, e.g. <c>anomalous-sign-in</c>.</param>
    /// <param name="logicalPath">The logical path within the detection package, e.g. <c>rule.kql</c> or <c>notes/investigation.md</c>.</param>
    /// <returns>The repository-relative path, e.g. <c>detections/anomalous-sign-in/rule.kql</c>.</returns>
    public static string Resolve(string detectionSlug, LogicalPath logicalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detectionSlug);
        ArgumentNullException.ThrowIfNull(logicalPath);

        return DetectionContentPathResolver.Resolve(
            DetectionSlug.Parse(detectionSlug),
            DetectionLogicalPath.Parse(logicalPath.Value));
    }

    /// <summary>
    /// Returns the repository-relative directory prefix for all files of a detection.
    /// </summary>
    public static string DetectionPrefix(string detectionSlug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detectionSlug);
        return DetectionContentPathResolver.DetectionPrefix(DetectionSlug.Parse(detectionSlug));
    }

    /// <summary>
    /// Given a repository-relative path, extracts the detection slug if it lives under
    /// <c>detections/</c>. Returns <c>null</c> for paths outside that tree.
    /// </summary>
    public static string? ExtractDetectionSlug(string repositoryPath) =>
        DetectionContentPathResolver.ExtractDetectionSlugValue(repositoryPath);
}
