namespace DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

public readonly record struct AgentId(Guid Value)
{
    public static AgentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
