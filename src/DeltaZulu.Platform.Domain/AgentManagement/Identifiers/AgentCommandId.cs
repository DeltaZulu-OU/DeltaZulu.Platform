namespace DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

public readonly record struct AgentCommandId(Guid Value)
{
    public static AgentCommandId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
