namespace DeltaZulu.DetectionContent.Paths;

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
        var slashIndex = afterRoot.IndexOf('/');
        if (slashIndex <= 0 || slashIndex == afterRoot.Length - 1)
        {
            throw new DetectionContentException("repository_path.shape", "Detection repository paths must use detections/<slug>/<logical-path>.");
        }

        var slug = DetectionSlug.Parse(afterRoot[..slashIndex]);
        var logicalPath = DetectionLogicalPath.Parse(afterRoot[(slashIndex + 1)..]);

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