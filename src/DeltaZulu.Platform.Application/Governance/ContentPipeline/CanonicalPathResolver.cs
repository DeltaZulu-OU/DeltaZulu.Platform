using DeltaZulu.Platform.Domain.Detection.Paths;
using DeltaZulu.Platform.Domain.Governance.Identifiers;

namespace DeltaZulu.Platform.Application.Governance.ContentPipeline;

/// <summary>
/// Governance compatibility adapter for the shared detection-content accepted path convention.
/// Shared products should prefer <see cref="DetectionContentPathResolver" /> directly once
/// Governance identifiers are translated to shared detection-content value objects.
/// </summary>
/// <remarks>
/// <para>Repository layout:</para>
/// <code>
/// detections/&lt;slug&gt;.yaml
/// detections/&lt;slug&gt;-rule.kql
/// detections/&lt;slug&gt;-tests-baseline.yaml
/// detections/&lt;slug&gt;-notes-investigation.md
/// detections/&lt;slug&gt;-notes-assets-timeline.png
/// </code>
/// <para>The public convention belongs to <see cref="DetectionContentPathResolver" />. This
/// adapter preserves the existing Governance API surface while delegating the convention to the
/// shared detection-content package. Governance domain validation should already have accepted
/// the slug and logical path before application services call this adapter.</para>
/// </remarks>
public static class CanonicalPathResolver
{
    /// <summary>
    /// Resolves a logical path within a detection to a repository-relative path.
    /// </summary>
    /// <param name="detectionSlug">The detection's slug, e.g. <c>anomalous-sign-in</c>.</param>
    /// <param name="logicalPath">The logical path within the detection package, e.g. <c>rule.kql</c> or <c>notes/investigation.md</c>.</param>
    /// <returns>The repository-relative path, e.g. <c>detections/anomalous-sign-in-rule.kql</c>.</returns>
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
    /// Converts a repository-relative accepted-content path for the supplied detection slug
    /// back to its logical package path, or <c>null</c> when the path belongs to another detection.
    /// </summary>
    public static string? TryGetLogicalPath(string detectionSlug, string repositoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detectionSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        var prefix = DetectionPrefix(detectionSlug);
        if (string.Equals(repositoryPath, prefix + ".yaml", StringComparison.Ordinal))
        {
            return "detection.yaml";
        }

        var flatPrefix = prefix + "-";
        if (!repositoryPath.StartsWith(flatPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var relative = repositoryPath[flatPrefix.Length..];
        return relative switch {
            "rule.kql" => "rule.kql",
            _ when relative.StartsWith("tests-", StringComparison.Ordinal) => "tests/" + relative["tests-".Length..],
            _ when relative.StartsWith("fixtures-", StringComparison.Ordinal) => "fixtures/" + relative["fixtures-".Length..],
            _ when relative.StartsWith("notes-assets-", StringComparison.Ordinal) => "notes/assets/" + relative["notes-assets-".Length..],
            _ when relative.StartsWith("notes-", StringComparison.Ordinal) => "notes/" + relative["notes-".Length..],
            _ => relative,
        };
    }

    /// <summary>
    /// Given a repository-relative path, extracts the detection slug if it lives under
    /// <c>detections/</c>. Returns <c>null</c> for paths outside that tree.
    /// </summary>
    public static string? ExtractDetectionSlug(string repositoryPath) =>
        DetectionContentPathResolver.ExtractDetectionSlugValue(repositoryPath);
}