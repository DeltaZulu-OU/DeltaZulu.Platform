namespace Workbench.Domain.Identifiers;

/// <summary>
/// Identifier for a <see cref="Detections.DetectionVersion"/>. Distinct from any Git commit SHA,
/// per ADR-0011: users see versions, not commits. The Git commit reference is stored on the
/// projection as advanced metadata only.
/// </summary>
public readonly record struct VersionId(Guid Value)
{
    public static VersionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}
