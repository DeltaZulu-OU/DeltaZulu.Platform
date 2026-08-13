namespace DeltaZulu.Platform.Domain.Detection.Paths;

/// <summary>Shared accepted-content repository path convention for detection packages.</summary>
public static class DetectionContentPathResolver
{
    /// <summary>Repository-relative root for accepted detections.</summary>
    public const string DetectionsRoot = "detections";

    /// <summary>Resolves a logical package path to a repository-relative accepted-content path.</summary>
    public static string Resolve(DetectionSlug detectionSlug, DetectionLogicalPath logicalPath)
    {
        ArgumentNullException.ThrowIfNull(detectionSlug);
        ArgumentNullException.ThrowIfNull(logicalPath);

        return $"{DetectionsRoot}/{BuildFileName(detectionSlug, logicalPath)}";
    }

    /// <summary>Returns the repository-relative prefix for all accepted files for a detection.</summary>
    public static string DetectionPrefix(DetectionSlug detectionSlug)
    {
        ArgumentNullException.ThrowIfNull(detectionSlug);
        return $"{DetectionsRoot}/{detectionSlug.Value}";
    }

    /// <summary>Extracts and validates a detection slug from a repository-relative accepted-content path when it is under <c>detections/</c>.</summary>
    public static DetectionSlug? ExtractDetectionSlug(string repositoryPath) =>
        TryExtractDetectionSlug(repositoryPath, out var slug) ? slug : null;

    /// <summary>Attempts to extract and validate a detection slug from a repository-relative accepted-content path.</summary>
    public static bool TryExtractDetectionSlug(string repositoryPath, out DetectionSlug? slug)
    {
        slug = null;

        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            return false;
        }

        if (!repositoryPath.StartsWith($"{DetectionsRoot}/", StringComparison.Ordinal))
        {
            return false;
        }

        var afterPrefix = repositoryPath[(DetectionsRoot.Length + 1)..];
        var slashIndex = afterPrefix.IndexOf('/');
        var rawSlug = slashIndex > 0
            ? afterPrefix[..slashIndex]
            : afterPrefix.EndsWith(".yaml", StringComparison.Ordinal)
                ? afterPrefix[..^".yaml".Length]
                : null;

        if (string.IsNullOrWhiteSpace(rawSlug))
        {
            return false;
        }

        try
        {
            slug = DetectionSlug.Parse(rawSlug);
            return true;
        }
        catch (DetectionContentException)
        {
            return false;
        }
    }

    /// <summary>Compatibility helper that returns the extracted detection slug value as a raw string.</summary>
    public static string? ExtractDetectionSlugValue(string repositoryPath) =>
        ExtractDetectionSlug(repositoryPath)?.Value;

    private static string BuildFileName(DetectionSlug detectionSlug, DetectionLogicalPath logicalPath)
    {
        if (string.Equals(logicalPath.Value, "detection.yaml", StringComparison.Ordinal))
        {
            return $"{detectionSlug.Value}.yaml";
        }

        var fileName = logicalPath.Value.Replace('/', '-');
        return $"{detectionSlug.Value}-{fileName}";
    }
}
