namespace DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

public readonly record struct ConfigVersionId(Guid Value)
{
    public static ConfigVersionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
