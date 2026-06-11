using System.Text.RegularExpressions;
using DeltaZulu.Platform.Domain.Workbench.Common;

namespace DeltaZulu.Platform.Domain.Workbench.Identifiers;

/// <summary>
/// A validated logical path inside a detection package, used as the key for draft files.
/// The canonical writer maps logical paths to repository paths at merge time; the format here
/// must already be safe against path traversal and unexpected separators.
/// </summary>
/// <remarks>
/// <para>Examples of accepted paths:</para>
/// <list type="bullet">
///   <item><description><c>detection.yaml</c></description></item>
///   <item><description><c>rule.kql</c></description></item>
///   <item><description><c>tests/baseline.yaml</c></description></item>
///   <item><description><c>fixtures/sign-in.ndjson</c></description></item>
/// </list>
/// <para>Rejected:</para>
/// <list type="bullet">
///   <item><description>absolute paths, backslashes, drive letters</description></item>
///   <item><description>path traversal segments (<c>.</c>, <c>..</c>)</description></item>
///   <item><description>empty segments, leading or trailing slashes</description></item>
///   <item><description>characters outside <c>[a-z0-9._-]</c> in any segment (uppercase rejected)</description></item>
/// </list>
/// </remarks>
public sealed partial class LogicalPath : IEquatable<LogicalPath>
{
    /// <summary>Maximum allowed length of the full logical path string.</summary>
    public const int MaxLength = 240;

    /// <summary>Maximum allowed length of any single path segment.</summary>
    public const int MaxSegmentLength = 80;

    private static readonly char[] Separator = ['/'];

    [GeneratedRegex(@"^[a-z0-9](?:[a-z0-9._-]{0,78}[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex SegmentPattern();

    /// <summary>The canonical string form, e.g. <c>tests/baseline.yaml</c>.</summary>
    public string Value { get; }

    private LogicalPath(string value)
    {
        Value = value;
    }

    /// <summary>Parses a logical path, throwing <see cref="DomainException"/> on rejection.</summary>
    /// <exception cref="DomainException">When the path is invalid.</exception>
    public static LogicalPath Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new DomainException("path.empty", "Logical path must not be empty.");
        }

        if (raw.Length > MaxLength)
        {
            throw new DomainException(
                "path.too_long",
                $"Logical path exceeds {MaxLength} characters.");
        }

        if (raw.Contains('\\', StringComparison.Ordinal))
        {
            throw new DomainException("path.backslash", "Backslashes are not allowed in logical paths.");
        }

        if (raw.StartsWith('/') || raw.EndsWith('/'))
        {
            throw new DomainException(
                "path.boundary_slash",
                "Logical paths must not start or end with '/'.");
        }

        foreach (var segment in raw.Split(Separator, StringSplitOptions.None))
        {
            if (segment.Length == 0)
            {
                throw new DomainException("path.empty_segment", "Logical path has an empty segment.");
            }

            if (segment is "." or "..")
            {
                throw new DomainException(
                    "path.traversal",
                    "Path traversal segments '.' and '..' are not allowed.");
            }

            if (segment.Length > MaxSegmentLength)
            {
                throw new DomainException(
                    "path.segment_too_long",
                    $"Path segment '{segment}' exceeds {MaxSegmentLength} characters.");
            }

            if (!SegmentPattern().IsMatch(segment))
            {
                throw new DomainException(
                    "path.segment_chars",
                    $"Path segment '{segment}' contains disallowed characters.");
            }
        }

        return new LogicalPath(raw);
    }

    public bool Equals(LogicalPath? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is LogicalPath p && Equals(p);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(LogicalPath? left, LogicalPath? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(LogicalPath? left, LogicalPath? right) => !(left == right);
}