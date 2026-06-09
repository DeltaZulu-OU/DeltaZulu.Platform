namespace DeltaZulu.DetectionContent.Identity;

/// <summary>Stable cross-product identity for an accepted detection-content version projection.</summary>
public sealed record DetectionContentVersionId
{
    /// <summary>Underlying GUID value.</summary>
    public Guid Value { get; }

    /// <summary>Creates a detection-content version identity from a non-empty GUID.</summary>
    public DetectionContentVersionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DetectionContentException("detection_content_version_id.empty", "Detection content version ID must not be empty.");
        }

        Value = value;
    }

    /// <summary>Creates a new detection-content version identity.</summary>
    public static DetectionContentVersionId New() => new(Guid.NewGuid());

    /// <summary>Creates a detection-content version identity from a non-empty GUID.</summary>
    public static DetectionContentVersionId From(Guid value) => new(value);

    /// <summary>Parses a detection-content version identity from its canonical GUID string form.</summary>
    public static DetectionContentVersionId Parse(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);
        return Guid.TryParse(raw, out var value)
            ? new DetectionContentVersionId(value)
            : throw new DetectionContentException("detection_content_version_id.invalid", "Detection content version ID must be a valid GUID.");
    }

    /// <inheritdoc />
    public override string ToString() => Value.ToString("D");
}
