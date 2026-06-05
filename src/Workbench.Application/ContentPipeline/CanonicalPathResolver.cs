using Workbench.Domain.Identifiers;

namespace Workbench.Application.ContentPipeline;

/// <summary>
/// Maps a detection slug and a <see cref="LogicalPath"/> to a repository-relative path
/// inside the accepted content store. Implements the path convention from ADR-0015.
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
/// <para>The resolver does not validate that the slug or logical path are well-formed;
/// that validation has already happened in the domain layer (<see cref="LogicalPath.Parse"/>
/// and <c>Detection.Conceive</c>).</para>
/// </remarks>
public static class CanonicalPathResolver
{
    private const string DetectionsRoot = "detections";

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

        return $"{DetectionsRoot}/{detectionSlug}/{logicalPath.Value}";
    }

    /// <summary>
    /// Returns the repository-relative directory prefix for all files of a detection.
    /// </summary>
    public static string DetectionPrefix(string detectionSlug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detectionSlug);
        return $"{DetectionsRoot}/{detectionSlug}";
    }

    /// <summary>
    /// Given a repository-relative path, extracts the detection slug if it lives under
    /// <c>detections/</c>. Returns <c>null</c> for paths outside that tree.
    /// </summary>
    public static string? ExtractDetectionSlug(string repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            return null;
        }

        if (!repositoryPath.StartsWith($"{DetectionsRoot}/", StringComparison.Ordinal))
        {
            return null;
        }

        var afterPrefix = repositoryPath[(DetectionsRoot.Length + 1)..];
        var slashIndex = afterPrefix.IndexOf('/');
        return slashIndex > 0 ? afterPrefix[..slashIndex] : null;
    }
}
