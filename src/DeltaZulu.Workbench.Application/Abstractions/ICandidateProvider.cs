namespace Workbench.Application.Abstractions;

/// <summary>
/// Port through which Workbench reads incident candidates from Hunting.
/// Hunting implements this contract; Workbench only consumes.
/// </summary>
public interface ICandidateProvider
{
    Task<CandidateReadModel?> GetByIdAsync(Guid candidateId, CancellationToken ct = default);

    Task<IReadOnlyList<CandidateReadModel>> ListPendingAsync(CancellationToken ct = default);

    Task<IReadOnlyList<CandidateReadModel>> ListByEntityAsync(
        string entityType, string entityValue, CancellationToken ct = default);
}
