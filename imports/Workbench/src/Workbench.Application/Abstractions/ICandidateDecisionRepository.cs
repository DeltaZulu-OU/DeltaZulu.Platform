using Workbench.Domain.Identifiers;
using Workbench.Domain.Triage;

namespace Workbench.Application.Abstractions;

public interface ICandidateDecisionRepository
{
    Task<CandidateDecision?> GetByIdAsync(CandidateDecisionId id, CancellationToken ct = default);

    Task<IReadOnlyList<CandidateDecision>> ListByCandidateAsync(Guid candidateId, CancellationToken ct = default);

    void Add(CandidateDecision decision);

    void Save(CandidateDecision decision);
}
