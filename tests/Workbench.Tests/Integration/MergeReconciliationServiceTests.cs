using Workbench.Application.Abstractions;
using Workbench.Application.Services;
using Workbench.Domain.Enums;
using Workbench.Domain.Identifiers;

namespace Workbench.Tests.Integration;

[TestClass]
public sealed class MergeReconciliationServiceTests : IDisposable
{
    private static readonly UserId Author = UserId.New();
    private readonly TestServiceProvider _host = new();

    [TestMethod]
    public async Task ListUnresolvedAsync_AfterSuccessfulMerge_DoesNotReportCompletedIntent()
    {
        var detectionId = await ConceiveDetectionAsync("merge-intent-completed");
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

    private async Task<DetectionId> ConceiveDetectionAsync(string slug)
    {
        using var scope = _host.CreateScope();
        var svc = _host.Resolve<DetectionContentService>(scope);
        return (await svc.ConceiveAsync(slug, "Merge Intent Test Detection", "", TestContext.CancellationToken)).Id;
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

    public void Dispose()
    {
        _host.Dispose();
    }

    public TestContext TestContext { get; set; }
}
