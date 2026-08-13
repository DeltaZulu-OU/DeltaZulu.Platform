namespace DeltaZulu.Platform.Domain.Analytics.Investigations;

public interface IInvestigationRepository
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
    Task SaveInvestigationAsync(InvestigationRecord investigation, CancellationToken cancellationToken = default);
    Task<InvestigationRecord?> GetInvestigationAsync(string investigationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvestigationRecord>> ListInvestigationsAsync(CancellationToken cancellationToken = default);
    Task SavePivotAsync(InvestigationPivotRecord pivot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvestigationPivotRecord>> ListPivotsAsync(string investigationId, CancellationToken cancellationToken = default);
    Task SaveQueryRunAsync(InvestigationQueryRunRecord run, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvestigationQueryRunRecord>> ListQueryRunsAsync(string investigationId, CancellationToken cancellationToken = default);
    Task SaveEvidenceAsync(IReadOnlyList<EvidenceRecord> evidence, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvidenceRecord>> ListEvidenceAsync(string investigationId, CancellationToken cancellationToken = default);
    Task AddTagsAsync(IReadOnlyList<EvidenceTagRecord> tags, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvidenceTagRecord>> ListTagsAsync(string investigationId, CancellationToken cancellationToken = default);
    Task AddCommentAsync(EvidenceCommentRecord comment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvidenceCommentRecord>> ListCommentsAsync(string investigationId, CancellationToken cancellationToken = default);
    Task AddEvidenceLinkAsync(EvidenceLinkRecord link, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvidenceLinkRecord>> ListEvidenceLinksAsync(string investigationId, CancellationToken cancellationToken = default);
    Task AddEntityLinkAsync(EvidenceEntityLinkRecord link, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvidenceEntityLinkRecord>> ListEntityLinksAsync(string investigationId, CancellationToken cancellationToken = default);
}
