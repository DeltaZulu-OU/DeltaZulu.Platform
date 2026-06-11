using System.Text.RegularExpressions;

namespace DeltaZulu.Platform.Domain.Detection.Paths;

/// <summary>Validated logical path inside a detection-content package.</summary>
public sealed partial class DetectionLogicalPath : IEquatable<DetectionLogicalPath>
{
    /// <summary>Maximum allowed full logical path length.</summary>
    public const int MaxLength = 240;

    /// <summary>Maximum allowed length of one path segment.</summary>
    public const int MaxSegmentLength = 80;

    private static readonly char[] Separator = ['/'];

    [GeneratedRegex(@"^[a-z0-9](?:[a-z0-9._-]{0,78}[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex SegmentPattern();

    /// <summary>Canonical path value.</summary>
    public string Value { get; }

    private DetectionLogicalPath(string value)
    {
        Value = value;
    }

    /// <summary>Parses a logical path and rejects traversal, absolute paths, empty segments, and unsafe characters.</summary>
    public static DetectionLogicalPath Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new DetectionContentException("path.empty", "Logical path must not be empty.");
        }

        if (raw.Length > MaxLength)
        {
            throw new DetectionContentException("path.too_long", $"Logical path exceeds {MaxLength} characters.");
        }

        if (raw.Contains('\\', StringComparison.Ordinal))
        {
            throw new DetectionContentException("path.backslash", "Backslashes are not allowed in logical paths.");
        }

        if (raw.StartsWith('/') || raw.EndsWith('/'))
        {
            throw new DetectionContentException("path.boundary_slash", "Logical paths must not start or end with '/'.");
        }

        foreach (var segment in raw.Split(Separator, StringSplitOptions.None))
        {
            ValidateSegment(segment);
        }

        return new DetectionLogicalPath(raw);
    }

    private static void ValidateSegment(string segment)
    {
        if (segment.Length == 0)
        {
            throw new DetectionContentException("path.empty_segment", "Logical path has an empty segment.");
        }

        if (segment is "." or "..")
        {
            throw new DetectionContentException("path.traversal", "Path traversal segments '.' and '..' are not allowed.");
        }

        if (segment.Length > MaxSegmentLength)
        {
            throw new DetectionContentException("path.segment_too_long", $"Path segment '{segment}' exceeds {MaxSegmentLength} characters.");
        }

        if (!SegmentPattern().IsMatch(segment))
        {
            throw new DetectionContentException("path.segment_chars", $"Path segment '{segment}' contains disallowed characters.");
        }
    }

    /// <inheritdoc />
    public bool Equals(DetectionLogicalPath? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DetectionLogicalPath path && Equals(path);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value;
}