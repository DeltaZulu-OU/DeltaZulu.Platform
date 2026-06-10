namespace DeltaZulu.Workbench.Domain.Identifiers;

/// <summary>Identifier for a user. Authentication backend is out of POC scope; this is the stable handle.</summary>
public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}