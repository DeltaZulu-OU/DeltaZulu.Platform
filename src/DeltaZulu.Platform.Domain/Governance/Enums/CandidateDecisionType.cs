namespace DeltaZulu.Platform.Domain.Governance.Enums;

public enum CandidateDecisionType
{
    Approve = 0,
    RejectFalsePositive = 1,
    RejectBenign = 2,
    Suppress = 3,
    RequestEvidence = 4,
}