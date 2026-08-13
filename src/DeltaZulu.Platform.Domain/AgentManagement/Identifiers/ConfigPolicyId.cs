namespace DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

public readonly record struct ConfigPolicyId(Guid Value)
{
    public static ConfigPolicyId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
