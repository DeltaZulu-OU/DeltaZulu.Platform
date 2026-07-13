namespace DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

public readonly record struct EnrollmentTokenId(Guid Value)
{
    public static EnrollmentTokenId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
