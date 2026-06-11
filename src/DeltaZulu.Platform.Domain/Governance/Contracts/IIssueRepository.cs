using DeltaZulu.Platform.Domain.Governance.Identifiers;
using DeltaZulu.Platform.Domain.Governance.Issues;

namespace DeltaZulu.Platform.Domain.Governance.Contracts;

public interface IIssueRepository
{
    Task<Issue?> GetByIdAsync(IssueId id, CancellationToken ct = default);

    Task<IReadOnlyList<Issue>> ListAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Issue>> ListOpenAsync(CancellationToken ct = default);

    void Add(Issue issue);

    void Save(Issue issue);
}