using Workbench.Domain.Identifiers;
using Workbench.Domain.Issues;

namespace Workbench.Application.Abstractions;

public interface IIssueRepository
{
    Task<Issue?> GetByIdAsync(IssueId id, CancellationToken ct = default);
    Task<IReadOnlyList<Issue>> ListAsync(CancellationToken ct = default);
    void Add(Issue issue);
    void Save(Issue issue);
}
