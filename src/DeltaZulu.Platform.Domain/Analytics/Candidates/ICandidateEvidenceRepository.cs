namespace DeltaZulu.Platform.Domain.Analytics.Candidates;

public interface ICandidateEvidenceRepository
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CandidateEvidenceRecord>> ListByCandidateAsync(string candidateId, CancellationToken cancellationToken = default);

    Task SaveAsync(CandidateEvidenceRecord evidence, CancellationToken cancellationToken = default);

    Task SaveBatchAsync(IReadOnlyList<CandidateEvidenceRecord> evidence, CancellationToken cancellationToken = default);
}
