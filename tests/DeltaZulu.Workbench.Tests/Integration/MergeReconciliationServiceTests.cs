using DeltaZulu.Workbench.Application.Abstractions;
using DeltaZulu.Workbench.Application.Services;
using DeltaZulu.Workbench.Domain.Enums;
using DeltaZulu.Workbench.Domain.Identifiers;

namespace DeltaZulu.Workbench.Tests.Integration;

[TestClass]
public sealed class MergeReconciliationServiceTests : IDisposable
{
    private static readonly UserId Author = UserId.New();
    private readonly TestServiceProvider _host = new();

    [TestMethod]
    public async Task ListUnresolvedAsync_AfterSuccessfulMerge_DoesNotReportCompletedIntent()
    {
        var detectionId = await CreateDetectionAsync("merge-intent-completed");
        var changeId = await OpenQuickLabChangeAsync(detectionId, "CHG-MI-1");

        using (var scope = _host.CreateScope())
        {
            var mergeSvc = _host.Resolve<MergeService>(scope);
            await mergeSvc.MergeAsync(changeId, "Merge Tester", "merge@example.test", TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var reconciliation = _host.Resolve<MergeReconciliationService>(scope);
            var unresolved = await reconciliation.ListUnresolvedAsync(TestContext.CancellationToken);

            Assert.HasCount(0, unresolved);
        }
    }

    [TestMethod]
    public async Task ListUnresolvedAsync_CommittedIntentWithoutProjection_IsReportedForRepair()
    {
        var changeId = ChangeRequestId.New();
        var detectionId = DetectionId.New();
        var requestedAt = new DateTimeOffset(2026, 2, 1, 10, 0, 0, TimeSpan.Zero);
        var committedAt = requestedAt.AddSeconds(5);

        using (var scope = _host.CreateScope())
        {
            var intents = _host.Resolve<IMergeIntentRepository>(scope);
            await intents.CreatePendingAsync(new MergeIntent(
                changeId,
                detectionId,
                "unprojected-detection",
                requestedAt,
                "Merge Tester",
                "merge@example.test",
                "[CHG-MI-2] Unprojected content",
                MergeIntentState.Pending), TestContext.CancellationToken);
            await intents.MarkCommittedAsync(changeId, "abc123", committedAt, TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var reconciliation = _host.Resolve<MergeReconciliationService>(scope);
            var unresolved = await reconciliation.ListUnresolvedAsync(TestContext.CancellationToken);

            Assert.HasCount(1, unresolved);
            var intent = unresolved.Single();
            Assert.AreEqual(MergeIntentState.Committed, intent.State);
            Assert.AreEqual(changeId, intent.ChangeId);
            Assert.AreEqual(detectionId, intent.DetectionId);
            Assert.AreEqual("unprojected-detection", intent.DetectionSlug);
            Assert.AreEqual("abc123", intent.CommitSha);
            Assert.AreEqual(committedAt, intent.CommittedAt);
            Assert.IsNull(intent.VersionId);
        }
    }

    [TestMethod]
    public async Task RepairCommittedAsync_CommittedIntentWithoutProjection_CreatesVersionProjection()
    {
        var detectionId = await CreateDetectionAsync("merge-intent-repair");
        var changeId = await OpenQuickLabChangeAsync(detectionId, "CHG-MI-3");
        CommitResult commitResult;

        using (var scope = _host.CreateScope())
        {
            var contentStore = _host.Resolve<IAcceptedContentStore>(scope);
            commitResult = await contentStore.CommitAsync(new CommitRequest(
                "[CHG-MI-3] Accepted content with failed projection",
                "Merge Tester",
                "merge@example.test",
                [new ContentFile("detections/merge-intent-repair/rule.kql", "SigninLogs | take 1")]),
                TestContext.CancellationToken);

            var intents = _host.Resolve<IMergeIntentRepository>(scope);
            await intents.CreatePendingAsync(new MergeIntent(
                changeId,
                detectionId,
                "merge-intent-repair",
                new DateTimeOffset(2026, 2, 1, 10, 0, 0, TimeSpan.Zero),
                "Merge Tester",
                "merge@example.test",
                "[CHG-MI-3] Accepted content with failed projection",
                MergeIntentState.Pending), TestContext.CancellationToken);
            await intents.MarkCommittedAsync(changeId, commitResult.CommitSha, commitResult.CommittedAt, TestContext.CancellationToken);
        }

        MergeRepairResult repair;
        using (var scope = _host.CreateScope())
        {
            var reconciliation = _host.Resolve<MergeReconciliationService>(scope);
            repair = await reconciliation.RepairCommittedAsync(changeId, TestContext.CancellationToken);
        }

        Assert.IsTrue(repair.IsSuccess);
        Assert.AreEqual(MergeRepairStatus.Repaired, repair.Status);
        Assert.IsNotNull(repair.VersionId);
        Assert.AreEqual(commitResult.CommitSha, repair.CommitSha);

        using (var scope = _host.CreateScope())
        {
            var reconciliation = _host.Resolve<MergeReconciliationService>(scope);
            var unresolved = await reconciliation.ListUnresolvedAsync(TestContext.CancellationToken);
            Assert.HasCount(0, unresolved);

            var versions = _host.Resolve<IDetectionVersionRepository>(scope);
            var version = await versions.GetByIdAsync(repair.VersionId.Value, TestContext.CancellationToken);
            Assert.IsNotNull(version);
            Assert.AreEqual(commitResult.CommitSha, version.GitCommitSha);
            Assert.AreEqual(changeId, version.SourceChangeRequestId);

            var changes = _host.Resolve<IChangeRequestRepository>(scope);
            var change = await changes.GetByIdAsync(changeId, TestContext.CancellationToken);
            Assert.IsNotNull(change);
            Assert.AreEqual(ChangeStatus.Merged, change.Status);
            Assert.AreEqual(version.Id, change.ResultVersionId);

            var detections = _host.Resolve<IDetectionRepository>(scope);
            var detection = await detections.GetByIdAsync(detectionId, TestContext.CancellationToken);
            Assert.IsNotNull(detection);
            Assert.AreEqual(version.Id, detection.CurrentVersionId);
        }
    }

    [TestMethod]
    public async Task RepairCommittedAsync_MissingAcceptedContentCommit_IsNotRepairable()
    {
        var changeId = ChangeRequestId.New();
        var detectionId = DetectionId.New();
        var requestedAt = new DateTimeOffset(2026, 2, 1, 10, 0, 0, TimeSpan.Zero);

        using (var scope = _host.CreateScope())
        {
            var intents = _host.Resolve<IMergeIntentRepository>(scope);
            await intents.CreatePendingAsync(new MergeIntent(
                changeId,
                detectionId,
                "missing-commit-detection",
                requestedAt,
                "Merge Tester",
                "merge@example.test",
                "[CHG-MI-4] Missing content commit",
                MergeIntentState.Pending), TestContext.CancellationToken);
            await intents.MarkCommittedAsync(changeId, "missing-sha", requestedAt.AddSeconds(5), TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var reconciliation = _host.Resolve<MergeReconciliationService>(scope);
            var repair = await reconciliation.RepairCommittedAsync(changeId, TestContext.CancellationToken);

            Assert.IsFalse(repair.IsSuccess);
            Assert.AreEqual(MergeRepairStatus.NotRepairable, repair.Status);
            StringAssert.Contains(repair.Message, "Accepted-content commit was not found");
        }
    }

    [TestMethod]
    public void BuildRecoveryGuidance_CommittedIntentWithSnapshot_AllowsProjectionRepair()
    {
        var changeId = ChangeRequestId.New();
        var intent = NewMergeIntent(changeId, MergeIntentState.Committed, commitSha: "abc123");

        var guidance = MergeReconciliationService.BuildRecoveryGuidance(intent);

        Assert.AreEqual(changeId, guidance.ChangeId);
        Assert.AreEqual(MergeRecoveryActionKind.RepairProjection, guidance.ActionKind);
        Assert.IsTrue(guidance.CanRepair);
        Assert.AreEqual("Repair projection", guidance.RecommendedAction);
        StringAssert.Contains(guidance.Message, "version projection");
    }

    [TestMethod]
    public void BuildRecoveryGuidance_PendingIntent_DisablesProjectionRepair()
    {
        var changeId = ChangeRequestId.New();
        var intent = NewMergeIntent(changeId, MergeIntentState.Pending);

        var guidance = MergeReconciliationService.BuildRecoveryGuidance(intent);

        Assert.AreEqual(changeId, guidance.ChangeId);
        Assert.AreEqual(MergeRecoveryActionKind.WaitingForAcceptedWrite, guidance.ActionKind);
        Assert.IsFalse(guidance.CanRepair);
        Assert.AreEqual("Wait or retry merge", guidance.RecommendedAction);
        StringAssert.Contains(guidance.Message, "accepted-content write has not completed");
    }

    [TestMethod]
    public void BuildRecoveryGuidance_CommittedIntentWithoutSnapshot_RequiresInvestigation()
    {
        var changeId = ChangeRequestId.New();
        var intent = NewMergeIntent(changeId, MergeIntentState.Committed);

        var guidance = MergeReconciliationService.BuildRecoveryGuidance(intent);

        Assert.AreEqual(changeId, guidance.ChangeId);
        Assert.AreEqual(MergeRecoveryActionKind.NeedsInvestigation, guidance.ActionKind);
        Assert.IsFalse(guidance.CanRepair);
        Assert.AreEqual("Verify accepted snapshot", guidance.RecommendedAction);
        StringAssert.Contains(guidance.Message, "no accepted snapshot is recorded");
    }

    private async Task<DetectionId> CreateDetectionAsync(string slug)
    {
        using var scope = _host.CreateScope();
        var svc = _host.Resolve<DetectionContentService>(scope);
        return (await svc.CreateAsync(slug, "Merge Intent Test Detection", "", TestContext.CancellationToken)).Id;
    }

    private async Task<ChangeRequestId> OpenQuickLabChangeAsync(DetectionId detectionId, string key)
    {
        using var scope = _host.CreateScope();
        var changeSvc = _host.Resolve<ChangeService>(scope);
        var change = await changeSvc.OpenChangeAsync(
            key,
            "Accepted content with merge intent",
            detectionId,
            Author,
            WorkflowProfileId.QuickLab,
            ct: TestContext.CancellationToken);

        await changeSvc.UpsertDraftFileAsync(
            change.Id,
            "rule.kql",
            DraftContentType.HuntingQuery,
            "SigninLogs | take 1",
            Author,
            TestContext.CancellationToken);

        return change.Id;
    }

    private static MergeIntent NewMergeIntent(
        ChangeRequestId changeId,
        MergeIntentState state,
        string? commitSha = null)
        => new(
            changeId,
            DetectionId.New(),
            "guidance-detection",
            new DateTimeOffset(2026, 2, 1, 10, 0, 0, TimeSpan.Zero),
            "Merge Tester",
            "merge@example.test",
            "[CHG-MI-G] Guidance test",
            state,
            commitSha,
            commitSha is null ? null : new DateTimeOffset(2026, 2, 1, 10, 0, 5, TimeSpan.Zero));

    public void Dispose()
    {
        _host.Dispose();
    }

    public TestContext TestContext { get; set; }
}
