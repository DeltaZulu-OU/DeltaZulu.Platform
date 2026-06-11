namespace DeltaZulu.Platform.Domain.Governance.Identifiers;

/// <summary>Identifier for a <see cref="Changes.ChangeRequest"/>.</summary>
public readonly record struct ChangeRequestId(Guid Value)
{
    public static ChangeRequestId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}