namespace DeltaZulu.DetectionContent.Identity;

/// <summary>Stable cross-product identity for a detection-content object.</summary>
public sealed record DetectionContentId
{
    /// <summary>Underlying GUID value.</summary>
    public Guid Value { get; }

    /// <summary>Creates a detection-content identity from a non-empty GUID.</summary>
    public DetectionContentId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DetectionContentException("detection_content_id.empty", "Detection content ID must not be empty.");
        }

        Value = value;
    }

    /// <summary>Creates a new detection-content identity.</summary>
    public static DetectionContentId New() => new(Guid.NewGuid());

    /// <summary>Creates a detection-content identity from a non-empty GUID.</summary>
    public static DetectionContentId From(Guid value) => new(value);

    /// <summary>Parses a detection-content identity from its canonical GUID string form.</summary>
    public static DetectionContentId Parse(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);
        return Guid.TryParse(raw, out var value)
            ? new DetectionContentId(value)
            : throw new DetectionContentException("detection_content_id.invalid", "Detection content ID must be a valid GUID.");
    }

    /// <inheritdoc />
    public override string ToString() => Value.ToString("D");
}
