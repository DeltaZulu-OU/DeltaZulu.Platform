using System.Text.RegularExpressions;

namespace DeltaZulu.DetectionContent.Paths;

/// <summary>Stable, URL- and path-safe key for accepted detection content.</summary>
public sealed partial class DetectionSlug : IEquatable<DetectionSlug>
{
    /// <summary>Maximum allowed slug length.</summary>
    public const int MaxLength = 64;

    [GeneratedRegex(@"^[a-z][a-z0-9-]{1,62}[a-z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    /// <summary>Canonical slug value.</summary>
    public string Value { get; }

    private DetectionSlug(string value)
    {
        Value = value;
    }

    /// <summary>Parses and validates a detection slug.</summary>
    public static DetectionSlug Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        if (!SlugPattern().IsMatch(raw))
        {
            throw new DetectionContentException(
                "detection.slug_invalid",
                $"Detection slug '{raw}' must be lowercase letters, digits, hyphens; 3–64 characters.");
        }

        return new DetectionSlug(raw);
    }

    /// <inheritdoc />
    public bool Equals(DetectionSlug? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DetectionSlug slug && Equals(slug);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value;
}
