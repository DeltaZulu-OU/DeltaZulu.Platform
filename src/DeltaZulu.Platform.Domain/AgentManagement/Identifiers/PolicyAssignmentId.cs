namespace DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

public readonly record struct PolicyAssignmentId(Guid Value)
{
    public static PolicyAssignmentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
