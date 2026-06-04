using Workbench.Application.Abstractions;
using Workbench.Domain.Common;
using Workbench.Domain.Enums;
using Workbench.Domain.Identifiers;
using Workbench.Domain.Issues;

namespace Workbench.Application.Services;

public sealed class IssueService(IIssueRepository issues, TimeProvider time)
{
    public Task<Issue> CreateIssueAsync(
        string key, string title, IssueType type, Priority priority,
        UserId? assigneeId = null, CancellationToken ct = default)
    {
        var issue = Issue.Create(IssueId.New(), key, title, type, priority, time.GetUtcNow(), assigneeId);
        issues.Add(issue);
        return Task.FromResult(issue);
    }

    public Task<Issue> CreateFromExternalCaseAsync(
        string key, string title, Priority priority,
        string externalSystem, string externalCaseId, string? externalCaseUrl = null,
        UserId? assigneeId = null, CancellationToken ct = default)
    {
        var now = time.GetUtcNow();
        var issue = Issue.Create(IssueId.New(), key, title, IssueType.Case, priority, now, assigneeId);
        issue.LinkExternalCase(new ExternalCaseRef(externalSystem, externalCaseId, externalCaseUrl), now);
        issues.Add(issue);
        return Task.FromResult(issue);
    }

    public async Task LinkExternalCaseAsync(
        IssueId issueId, string externalSystem, string externalCaseId,
        string? externalCaseUrl = null, CancellationToken ct = default)
    {
        var issue = await GetOrThrowAsync(issueId, ct);
        issue.LinkExternalCase(new ExternalCaseRef(externalSystem, externalCaseId, externalCaseUrl), time.GetUtcNow());
        issues.Save(issue);
    }

    public async Task LinkDetectionAsync(
        IssueId issueId, DetectionId detectionId, CancellationToken ct = default)
    {
        var issue = await GetOrThrowAsync(issueId, ct);
        issue.LinkDetection(detectionId, time.GetUtcNow());
        issues.Save(issue);
    }

    public Task<Issue?> GetByIdAsync(IssueId id, CancellationToken ct = default)
        => issues.GetByIdAsync(id, ct);

    public Task<IReadOnlyList<Issue>> ListAsync(CancellationToken ct = default)
        => issues.ListAsync(ct);

    private async Task<Issue> GetOrThrowAsync(IssueId id, CancellationToken ct)
        => await issues.GetByIdAsync(id, ct)
           ?? throw new DomainException("issue.not_found", $"Issue '{id}' not found.");
}