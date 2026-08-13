namespace DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

public readonly record struct ProfileVersionId(Guid Value)
{
    public static ProfileVersionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
