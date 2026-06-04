namespace Workbench.Domain.Identifiers;

/// <summary>Identifier for a <see cref="Detections.Detection"/>.</summary>
public readonly record struct DetectionId(Guid Value)
{
    public static DetectionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}