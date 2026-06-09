namespace DeltaZulu.Workbench.Domain.Identifiers;

public readonly record struct CandidateDecisionId(Guid Value)
{
    public static CandidateDecisionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
