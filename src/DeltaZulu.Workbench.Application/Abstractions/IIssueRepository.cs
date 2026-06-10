using DeltaZulu.Workbench.Domain.Identifiers;
using DeltaZulu.Workbench.Domain.Issues;

namespace DeltaZulu.Workbench.Application.Abstractions;

public interface IIssueRepository
{
    Task<Issue?> GetByIdAsync(IssueId id, CancellationToken ct = default);

    Task<IReadOnlyList<Issue>> ListAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Issue>> ListOpenAsync(CancellationToken ct = default);

    void Add(Issue issue);

    void Save(Issue issue);
}