namespace DeltaZulu.Platform.Domain.Detection.Paths;

/// <summary>Validated repository-relative accepted-content path for a detection package file.</summary>
public sealed class DetectionRepositoryPath : IEquatable<DetectionRepositoryPath>
{
    /// <summary>Canonical repository-relative path.</summary>
    public string Value { get; }

    /// <summary>Detection slug extracted from the repository path.</summary>
    public DetectionSlug Slug { get; }

    /// <summary>Logical path inside the detection package.</summary>
    public DetectionLogicalPath LogicalPath { get; }

    private DetectionRepositoryPath(string value, DetectionSlug slug, DetectionLogicalPath logicalPath)
    {
        Value = value;
        Slug = slug;
        LogicalPath = logicalPath;
    }

    /// <summary>Parses and validates a repository-relative accepted-content path under <c>detections/</c>.</summary>
    public static DetectionRepositoryPath Parse(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);

        if (raw.Contains('\\', StringComparison.Ordinal))
        {
            throw new DetectionContentException("repository_path.backslash", "Backslashes are not allowed in repository paths.");
        }

        if (raw.StartsWith('/') || raw.EndsWith('/'))
        {
            throw new DetectionContentException("repository_path.boundary_slash", "Repository paths must not start or end with '/'.");
        }

        if (!raw.StartsWith($"{DetectionContentPathResolver.DetectionsRoot}/", StringComparison.Ordinal))
        {
            throw new DetectionContentException("repository_path.root", "Detection repository paths must live under the 'detections/' root.");
        }

        var afterRoot = raw[(DetectionContentPathResolver.DetectionsRoot.Length + 1)..];
        foreach (var segment in afterRoot.Split('/'))
        {
            if (segment is "." or "..")
            {
                throw new DetectionContentException("path.traversal", "Path traversal segments '.' and '..' are not allowed.");
            }
        }

        if (!afterRoot.EndsWith(".yaml", StringComparison.Ordinal) || afterRoot.Contains('/', StringComparison.Ordinal))
        {
            throw new DetectionContentException("repository_path.shape", "Detection repository paths must use detections/<slug>.yaml.");
        }

        var slug = DetectionSlug.Parse(afterRoot[..^".yaml".Length]);
        var logicalPath = DetectionLogicalPath.Parse("detection.yaml");

        return new DetectionRepositoryPath(
            DetectionContentPathResolver.Resolve(slug, logicalPath),
            slug,
            logicalPath);
    }

    /// <inheritdoc />
    public bool Equals(DetectionRepositoryPath? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DetectionRepositoryPath path && Equals(path);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value;
}