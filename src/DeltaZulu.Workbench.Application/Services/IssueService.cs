using DeltaZulu.Workbench.Application.Abstractions;
using DeltaZulu.Workbench.Domain.Common;
using DeltaZulu.Workbench.Domain.Enums;
using DeltaZulu.Workbench.Domain.Identifiers;
using DeltaZulu.Workbench.Domain.Issues;

namespace DeltaZulu.Workbench.Application.Services;

public sealed class IssueService(IIssueRepository issues, TimeProvider time)
{
    public Task<Issue> CreateIssueAsync(
        string key, string title, IssueType type, CancellationToken ct = default)
    {
        var issue = Issue.Create(IssueId.New(), key, title, type, time.GetUtcNow());
        issues.Add(issue);
        return Task.FromResult(issue);
    }

    public Task<Issue> CreateFromExternalCaseAsync(
        string key, string title,
        string externalSystem, string externalCaseId, string? externalCaseUrl = null,
        ExternalSystemType systemType = ExternalSystemType.Generic,
        CancellationToken ct = default)
    {
        var now = time.GetUtcNow();
        var issue = Issue.Create(IssueId.New(), key, title, IssueType.Case, now);
        issue.LinkExternalCase(new ExternalCaseRef(externalSystem, externalCaseId, externalCaseUrl, systemType), now);
        issues.Add(issue);
        return Task.FromResult(issue);
    }

    public async Task LinkExternalCaseAsync(
        IssueId issueId, string externalSystem, string externalCaseId,
        string? externalCaseUrl = null, ExternalSystemType systemType = ExternalSystemType.Generic,
        CancellationToken ct = default)
    {
        var issue = await GetOrThrowAsync(issueId, ct);
        issue.LinkExternalCase(new ExternalCaseRef(externalSystem, externalCaseId, externalCaseUrl, systemType), time.GetUtcNow());
        issues.Save(issue);
    }

    public Task<Issue?> GetByIdAsync(IssueId id, CancellationToken ct = default)
        => issues.GetByIdAsync(id, ct);

    public Task<IReadOnlyList<Issue>> ListAsync(CancellationToken ct = default)
        => issues.ListAsync(ct);

    public Task<IReadOnlyList<Issue>> ListOpenAsync(CancellationToken ct = default)
        => issues.ListOpenAsync(ct);

    // --- lifecycle actions -----------------------------------------------------------------

    public Task RequestInfoAsync(IssueId id, CancellationToken ct = default)
        => RunAsync(id, static (i, now) => i.RequestInfo(now), ct);

    public Task ProvideInfoAsync(IssueId id, CancellationToken ct = default)
        => RunAsync(id, static (i, now) => i.ProvideInfo(now), ct);

    public Task TriageAsync(IssueId id, CancellationToken ct = default)
        => RunAsync(id, static (i, now) => i.Triage(now), ct);

    public Task RequireSanitizationAsync(IssueId id, CancellationToken ct = default)
        => RunAsync(id, static (i, now) => i.RequireSanitization(now), ct);

    public Task SanitizeAsync(IssueId id, CancellationToken ct = default)
        => RunAsync(id, static (i, now) => i.Sanitize(now), ct);

    public Task AcceptAsync(IssueId id, CancellationToken ct = default)
        => RunAsync(id, static (i, now) => i.Accept(now), ct);

    public Task RejectAsync(IssueId id, CancellationToken ct = default)
        => RunAsync(id, static (i, now) => i.Reject(now), ct);

    public Task DefineAcceptanceCriteriaAsync(IssueId id, string criteria, CancellationToken ct = default)
        => RunAsync(id, (i, now) => i.DefineAcceptanceCriteria(criteria, now), ct);

    public Task BlockAsync(IssueId id, CancellationToken ct = default)
        => RunAsync(id, static (i, now) => i.Block(now), ct);

    public Task UnblockAsync(IssueId id, CancellationToken ct = default)
        => RunAsync(id, static (i, now) => i.Unblock(now), ct);

    public Task StartWorkAsync(IssueId id, CancellationToken ct = default)
        => RunAsync(id, static (i, now) => i.StartWork(now), ct);

    public Task SubmitForReviewAsync(IssueId id, CancellationToken ct = default)
        => RunAsync(id, static (i, now) => i.SubmitForReview(now), ct);

    public Task RequestChangesAsync(IssueId id, CancellationToken ct = default)
        => RunAsync(id, static (i, now) => i.RequestChanges(now), ct);

    public Task CompleteAsync(IssueId id, CancellationToken ct = default)
        => RunAsync(id, static (i, now) => i.Complete(now), ct);

    public Task PublishAsync(IssueId id, CancellationToken ct = default)
        => RunAsync(id, static (i, now) => i.Publish(now), ct);

    public Task CloseAsync(IssueId id, CancellationToken ct = default)
        => RunAsync(id, static (i, now) => i.Close(now), ct);

    public Task ClassifyAsync(IssueId id, TlpLevel? tlp, IReadOnlyList<string>? labels, CancellationToken ct = default)
        => RunAsync(id, (i, now) => i.Classify(tlp, labels, now), ct);

    // --- helpers ---------------------------------------------------------------------------

    private async Task RunAsync(IssueId id, Action<Issue, DateTimeOffset> action, CancellationToken ct)
    {
        var issue = await GetOrThrowAsync(id, ct);
        action(issue, time.GetUtcNow());
        issues.Save(issue);
    }

    private async Task<Issue> GetOrThrowAsync(IssueId id, CancellationToken ct)
        => await issues.GetByIdAsync(id, ct)
           ?? throw new DomainException("issue.not_found", $"Issue '{id}' not found.");
}