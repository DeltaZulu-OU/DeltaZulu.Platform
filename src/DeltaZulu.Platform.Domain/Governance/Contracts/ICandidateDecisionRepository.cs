using DeltaZulu.Platform.Domain.Governance.Identifiers;
using DeltaZulu.Platform.Domain.Governance.Triage;

namespace DeltaZulu.Platform.Domain.Governance.Contracts;

public interface ICandidateDecisionRepository
{
    Task<CandidateDecision?> GetByIdAsync(CandidateDecisionId id, CancellationToken ct = default);

    Task<IReadOnlyList<CandidateDecision>> ListByCandidateAsync(Guid candidateId, CancellationToken ct = default);

    void Add(CandidateDecision decision);

    void Save(CandidateDecision decision);
}