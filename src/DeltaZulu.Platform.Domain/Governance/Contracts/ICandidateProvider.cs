namespace DeltaZulu.Platform.Domain.Governance.Contracts;

/// <summary>
/// Port through which Governance reads incident candidates from Analytics.
/// Analytics implements this contract; Governance only consumes.
/// </summary>
public interface ICandidateProvider
{
    Task<CandidateReadModel?> GetByIdAsync(Guid candidateId, CancellationToken ct = default);

    Task<IReadOnlyList<CandidateReadModel>> ListPendingAsync(CancellationToken ct = default);

    Task<IReadOnlyList<CandidateReadModel>> ListByEntityAsync(
        string entityType, string entityValue, CancellationToken ct = default);
}