using Workbench.Application.Services;

namespace Workbench.Tests.Integration;

[TestClass]
public sealed class ChangeServiceTests : IDisposable
{
    private TestServiceProvider _host = null!;
    private static readonly UserId Author = UserId.New();
    private static readonly UserId Reviewer = UserId.New();

    [TestInitialize]
    public void Setup() => _host = new TestServiceProvider();

    [TestCleanup]
    public void Teardown() => _host.Dispose();

    private async Task<DetectionId> ConceiveDetection(string slug = "test-det")
    {
        using var scope = _host.CreateScope();
        var svc = _host.Resolve<DetectionContentService>(scope);
        var det = await svc.ConceiveAsync(slug, "Test Detection", "For integration tests", TestContext.CancellationToken);
        return det.Id;
    }

    [TestMethod]
    public async Task OpenChangeAsync_PersistsChange_WithBaseVersionNull_ForNewDetection()
    {
        var detId = await ConceiveDetection();
        ChangeRequestId changeId;

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var change = await svc.OpenChangeAsync("CHG-001", "Initial detection", detId, Author, WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            changeId = change.Id;
            Assert.AreEqual(ChangeStatus.Draft, change.Status);
            Assert.IsNull(change.BaseVersionId, "Net-new detection has no base version.");
        }

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var loaded = await svc.GetByIdAsync(changeId, TestContext.CancellationToken);
            Assert.IsNotNull(loaded);
            Assert.AreEqual("CHG-001", loaded.Key);
            Assert.AreEqual(detId, loaded.DetectionId);
        }
    }

    [TestMethod]
    public async Task OpenChangeAsync_NonExistentDetection_Throws()
    {
        using var scope = _host.CreateScope();
        var svc = _host.Resolve<ChangeService>(scope);

        var ex = await Assert.ThrowsExactlyAsync<DomainException>(
            () => svc.OpenChangeAsync("CHG-X", "Ghost", DetectionId.New(), Author, WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken));
        Assert.AreEqual("detection.not_found", ex.Code);
    }

    [TestMethod]
    public async Task UpsertDraftFileAsync_CreatesAndUpdatesFiles_PersistsAcrossScopes()
    {
        var detId = await ConceiveDetection();
        ChangeRequestId changeId;

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var change = await svc.OpenChangeAsync("CHG-002", "Add KQL", detId, Author, WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            changeId = change.Id;
        }

        // Scope 2: add two draft files.
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            await svc.UpsertDraftFileAsync(changeId, "detection.yaml", DraftContentType.DetectionMetadata, "id: test-det", Author, TestContext.CancellationToken);
            await svc.UpsertDraftFileAsync(changeId, "rule.kql", DraftContentType.HuntingQuery, "SigninLogs | take 1", Author, TestContext.CancellationToken);
        }

        // Scope 3: read back files.
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var loaded = await svc.GetByIdAsync(changeId, TestContext.CancellationToken);
            Assert.IsNotNull(loaded);
            Assert.HasCount(2, loaded.DraftFiles);
            Assert.Contains(f => f.Path.Value == "detection.yaml", loaded.DraftFiles);
            Assert.Contains(f => f.Path.Value == "rule.kql", loaded.DraftFiles);
        }

        // Scope 4: upsert (update) existing file.
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            await svc.UpsertDraftFileAsync(changeId, "rule.kql", DraftContentType.HuntingQuery, "SigninLogs | where ResultType == 50074", Author, TestContext.CancellationToken);
        }

        // Scope 5: verify updated content.
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var loaded = await svc.GetByIdAsync(changeId, TestContext.CancellationToken);
            Assert.IsNotNull(loaded);
            Assert.HasCount(2, loaded.DraftFiles);
            var kql = loaded.DraftFiles.Single(f => f.Path.Value == "rule.kql");
            Assert.IsTrue(kql.Content.Contains("50074", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public async Task LinkIssueAsync_PersistsLink()
    {
        var detId = await ConceiveDetection();
        ChangeRequestId changeId;
        IssueId issueId;

        using (var scope = _host.CreateScope())
        {
            var issueSvc = _host.Resolve<IssueService>(scope);
            var issue = await issueSvc.CreateIssueAsync("ISS-100", "Linked bug", IssueType.Bug, Priority.Normal, ct: TestContext.CancellationToken);
            issueId = issue.Id;
        }

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var change = await svc.OpenChangeAsync("CHG-003", "Fix bug", detId, Author, WorkflowProfileId.ControlledReview, ct: TestContext.CancellationToken);
            changeId = change.Id;
        }

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            await svc.LinkIssueAsync(changeId, issueId, TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var loaded = await svc.GetByIdAsync(changeId, TestContext.CancellationToken);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(issueId, loaded.LinkedIssueId);
        }
    }

    [TestMethod]
    public async Task RecordReviewAsync_SelfApprovalBlock_SurvivesRoundTrip()
    {
        var detId = await ConceiveDetection("self-appr-det");
        ChangeRequestId changeId;

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var change = await svc.OpenChangeAsync("CHG-004", "Controlled change", detId, Author, WorkflowProfileId.ControlledReview, ct: TestContext.CancellationToken);
            changeId = change.Id;
        }

        // Self-approval attempt — the domain invariant must fire even after persistence round-trip.
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);

            var ex = await Assert.ThrowsExactlyAsync<DomainException>(
                () => svc.RecordReviewAsync(changeId, Author, ReviewDecision.Approved, "lgtm", TestContext.CancellationToken));
            Assert.AreEqual("review.self_approval_forbidden", ex.Code);
        }

        // Non-author approval succeeds.
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var review = await svc.RecordReviewAsync(changeId, Reviewer, ReviewDecision.Approved, "looks good", TestContext.CancellationToken);
            Assert.AreEqual(ReviewDecision.Approved, review.Decision);
        }

        // Verify review persisted.
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var loaded = await svc.GetByIdAsync(changeId, TestContext.CancellationToken);
            Assert.IsNotNull(loaded);
            Assert.HasCount(1, loaded.Reviews);
            Assert.AreEqual(Reviewer, loaded.Reviews[0].ReviewerId);
        }
    }

    [TestMethod]
    public async Task SelectWorkflowProfileAsync_LockedAfterReview()
    {
        var detId = await ConceiveDetection("profile-lock-det");
        ChangeRequestId changeId;

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var change = await svc.OpenChangeAsync("CHG-005", "Profile test", detId, Author, WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            changeId = change.Id;
        }

        // Record a comment review.
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            await svc.RecordReviewAsync(changeId, Reviewer, ReviewDecision.Commented, "taking a look", TestContext.CancellationToken);
        }

        // Attempt to change profile after a review exists — domain invariant fires.
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);

            var ex = await Assert.ThrowsExactlyAsync<DomainException>(
                () => svc.SelectWorkflowProfileAsync(changeId, WorkflowProfileId.ControlledReview, TestContext.CancellationToken));
            Assert.AreEqual("change.profile_locked", ex.Code);
        }
    }

    [TestMethod]
    public async Task CloseAsync_PersistsTerminalState()
    {
        var detId = await ConceiveDetection("close-det");
        ChangeRequestId changeId;

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var change = await svc.OpenChangeAsync("CHG-006", "Will close", detId, Author, WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            changeId = change.Id;
        }

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            await svc.CloseAsync(changeId, "No longer needed", TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var loaded = await svc.GetByIdAsync(changeId, TestContext.CancellationToken);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(ChangeStatus.Closed, loaded.Status);
            Assert.AreEqual("No longer needed", loaded.CloseReason);
        }
    }

    [TestMethod]
    public async Task ListAsync_ReturnsMultipleChanges()
    {
        var detId = await ConceiveDetection("list-det");

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            await svc.OpenChangeAsync("CHG-A", "First", detId, Author, WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            await svc.OpenChangeAsync("CHG-B", "Second", detId, Author, WorkflowProfileId.ControlledReview, ct: TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var list = await svc.ListAsync(TestContext.CancellationToken);
            Assert.HasCount(2, list);
        }
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    public TestContext TestContext { get; set; }
}