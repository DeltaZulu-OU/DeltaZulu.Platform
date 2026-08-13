namespace DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

public readonly record struct PolicyBundleId(Guid Value)
{
    public static PolicyBundleId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
