namespace DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

public readonly record struct ResourceProfileId(Guid Value)
{
    public static ResourceProfileId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
