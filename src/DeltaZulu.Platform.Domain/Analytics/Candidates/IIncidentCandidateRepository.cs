namespace DeltaZulu.Platform.Domain.Analytics.Candidates;

public interface IIncidentCandidateRepository
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncidentCandidateRecord>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncidentCandidateRecord>> ListByStatusAsync(string status, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncidentCandidateRecord>> ListByEntityAsync(string entityType, string entityValue, CancellationToken cancellationToken = default);

    Task<IncidentCandidateRecord?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task SaveAsync(IncidentCandidateRecord candidate, CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(string id, string status, DateTime updatedAtUtc, CancellationToken cancellationToken = default);

    Task SaveAlertLinksAsync(IReadOnlyList<CandidateAlertLink> links, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CandidateAlertLink>> ListAlertLinksAsync(string candidateId, CancellationToken cancellationToken = default);
}