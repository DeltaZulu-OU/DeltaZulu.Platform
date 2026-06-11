namespace DeltaZulu.Platform.Domain.Governance.Identifiers;

/// <summary>Identifier for a <see cref="Changes.CheckRun"/>.</summary>
public readonly record struct CheckRunId(Guid Value)
{
    public static CheckRunId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}