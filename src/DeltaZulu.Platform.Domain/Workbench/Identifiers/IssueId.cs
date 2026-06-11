namespace DeltaZulu.Platform.Domain.Workbench.Identifiers;

/// <summary>Identifier for an <see cref="Issues.Issue"/>.</summary>
public readonly record struct IssueId(Guid Value)
{
    public static IssueId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}