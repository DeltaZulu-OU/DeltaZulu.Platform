using DeltaZulu.Platform.Domain.Workbench.Identifiers;
using DeltaZulu.Platform.Domain.Workbench.Issues;

namespace DeltaZulu.Platform.Domain.Workbench.Contracts;

public interface IIssueRepository
{
    Task<Issue?> GetByIdAsync(IssueId id, CancellationToken ct = default);

    Task<IReadOnlyList<Issue>> ListAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Issue>> ListOpenAsync(CancellationToken ct = default);

    void Add(Issue issue);

    void Save(Issue issue);
}