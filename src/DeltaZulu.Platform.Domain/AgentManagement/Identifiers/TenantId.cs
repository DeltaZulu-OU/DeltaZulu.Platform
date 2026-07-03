namespace DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

public readonly record struct TenantId(Guid Value)
{
    public static TenantId Default => new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    public static TenantId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
