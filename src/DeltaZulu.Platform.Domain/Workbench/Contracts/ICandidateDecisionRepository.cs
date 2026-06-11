using DeltaZulu.Platform.Domain.Workbench.Identifiers;
using DeltaZulu.Platform.Domain.Workbench.Triage;

namespace DeltaZulu.Platform.Domain.Workbench.Contracts;

public interface ICandidateDecisionRepository
{
    Task<CandidateDecision?> GetByIdAsync(CandidateDecisionId id, CancellationToken ct = default);

    Task<IReadOnlyList<CandidateDecision>> ListByCandidateAsync(Guid candidateId, CancellationToken ct = default);

    void Add(CandidateDecision decision);

    void Save(CandidateDecision decision);
}