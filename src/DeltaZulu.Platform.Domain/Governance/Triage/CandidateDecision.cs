using DeltaZulu.Platform.Domain.Governance.Common;
using DeltaZulu.Platform.Domain.Governance.Enums;
using DeltaZulu.Platform.Domain.Governance.Identifiers;

namespace DeltaZulu.Platform.Domain.Governance.Triage;

/// <summary>
/// Records an analyst's triage decision on an incident candidate produced by Analytics.
/// Governance owns the decision; Analytics owns the candidate.
/// </summary>
public sealed class CandidateDecision : Entity<CandidateDecisionId>
{
    public Guid CandidateId { get; }
    public CandidateDecisionType Type { get; }
    public UserId AnalystId { get; }
    public string Reason { get; }
    public IncidentId? ResultingIncidentId { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    private CandidateDecision(
        CandidateDecisionId id,
        Guid candidateId,
        CandidateDecisionType type,
        UserId analystId,
        string reason,
        DateTimeOffset createdAt)
        : base(id)
    {
        CandidateId = candidateId;
        Type = type;
        AnalystId = analystId;
        Reason = reason;
        CreatedAt = createdAt;
    }

    public static CandidateDecision Record(
        CandidateDecisionId id,
        Guid candidateId,
        CandidateDecisionType type,
        UserId analystId,
        string reason,
        DateTimeOffset now) => reason?.Length > 4000
            ? throw new DomainException("decision.reason_too_long", "Decision reason exceeds 4000 characters.")
            : new CandidateDecision(id, candidateId, type, analystId, reason ?? string.Empty, now);

    public void LinkIncident(IncidentId incidentId)
    {
        if (Type != CandidateDecisionType.Approve)
        {
            throw new DomainException("decision.not_approval", "Only approval decisions create incidents.");
        }

        if (ResultingIncidentId is not null)
        {
            throw new DomainException("decision.already_linked", "Decision is already linked to an incident.");
        }

        ResultingIncidentId = incidentId;
    }

    public static CandidateDecision Reconstitute(
        CandidateDecisionId id,
        Guid candidateId,
        CandidateDecisionType type,
        UserId analystId,
        string reason,
        IncidentId? resultingIncidentId,
        DateTimeOffset createdAt) => new CandidateDecision(id, candidateId, type, analystId, reason, createdAt) {
            ResultingIncidentId = resultingIncidentId
        };
}