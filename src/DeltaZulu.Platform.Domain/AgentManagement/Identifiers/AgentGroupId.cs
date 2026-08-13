namespace DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

public readonly record struct AgentGroupId(Guid Value)
{
    public static AgentGroupId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
