namespace DeltaZulu.Platform.Domain.Workbench.Identifiers;

/// <summary>Identifier for a <see cref="Reviews.Review"/>.</summary>
public readonly record struct ReviewId(Guid Value)
{
    public static ReviewId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}