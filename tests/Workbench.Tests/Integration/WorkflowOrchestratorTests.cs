using Workbench.Application.Abstractions;
using Workbench.Application.Services;
using Workbench.Domain.Common;

namespace Workbench.Tests.Integration;

[TestClass]
public sealed class WorkflowOrchestratorTests : IDisposable
{
    private TestServiceProvider _host = null!;
    private static readonly UserId Author = UserId.New();
    private static readonly UserId Reviewer = UserId.New();

    [TestInitialize]
    public void Setup() => _host = new TestServiceProvider();

    [TestCleanup]
    public void Teardown() => _host.Dispose();

    private async Task<DetectionId> ConceiveDetection(string slug = "orch-det")
    {
        using var scope = _host.CreateScope();
        var svc = _host.Resolve<DetectionContentService>(scope);
        return (await svc.ConceiveAsync(slug, "Orchestrator Test", "", TestContext.CancellationToken)).Id;
    }

    [TestMethod]
    public async Task FullLifecycle_QuickLab_OrchestratorDispatchesWithoutError()
    {
        // This test exercises the full lifecycle through the application services,
        // verifying that the DomainDrivenOrchestrator dispatch points don't throw
        // and the domain state machine advances correctly.

        var detId = await ConceiveDetection();
        ChangeRequestId changeId;

        // 1. Open change → OnChangeOpened dispatched.
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var c = await svc.OpenChangeAsync("CHG-O1", "Orchestrated change", detId, Author, WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            changeId = c.Id;
        }

        // 2. Edit content → OnContentEdited dispatched.
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            await svc.UpsertDraftFileAsync(changeId, "detection.yaml", DraftContentType.DetectionMetadata,
                "id: orch-det\ntitle: t\ndescription: d\nseverity: low\n", Author, TestContext.CancellationToken);
        }

        // 3. Run checks → OnChecksCompleted dispatched.
        using (var scope = _host.CreateScope())
        {
            var runner = _host.Resolve<CheckPipelineRunner>(scope);
            await runner.RunAsync(changeId, TestContext.CancellationToken);
        }

        // 4. Record review → OnReviewRecorded dispatched.
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            await svc.RecordReviewAsync(changeId, Reviewer, ReviewDecision.Approved, "lgtm", TestContext.CancellationToken);
        }

        // 5. Merge → OnMergeCompleted dispatched.
        using (var scope = _host.CreateScope())
        {
            var mergeSvc = _host.Resolve<MergeService>(scope);
            var version = await mergeSvc.MergeAsync(changeId, "u", "e@e.com", TestContext.CancellationToken);
            Assert.AreEqual(1, version.SequenceNumber);
        }

        // Verify terminal state.
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var loaded = await svc.GetByIdAsync(changeId, TestContext.CancellationToken);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(ChangeStatus.Merged, loaded.Status);
        }
    }

    [TestMethod]
    public async Task CloseLifecycle_OrchestratorDispatchesOnChangeClosed()
    {
        var detId = await ConceiveDetection("close-orch");
        ChangeRequestId changeId;

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var c = await svc.OpenChangeAsync("CHG-O2", "Will close", detId, Author, WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            changeId = c.Id;
        }

        // Close → OnChangeClosed dispatched.
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
        }
    }

    [TestMethod]
    public async Task ControlledReview_FullLifecycle_WithOrchestrator()
    {
        var detId = await ConceiveDetection("ctrl-orch");
        ChangeRequestId changeId;

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var c = await svc.OpenChangeAsync("CHG-O3", "Controlled", detId, Author, WorkflowProfileId.ControlledReview, ct: TestContext.CancellationToken);
            changeId = c.Id;
            await svc.UpsertDraftFileAsync(changeId, "detection.yaml", DraftContentType.DetectionMetadata,
                "id: ctrl-orch\ntitle: t\ndescription: d\nseverity: high\n", Author, TestContext.CancellationToken);
        }

        // Run checks.
        using (var scope = _host.CreateScope())
        {
            var runner = _host.Resolve<CheckPipelineRunner>(scope);
            await runner.RunAsync(changeId, TestContext.CancellationToken);
        }

        // Non-author approval.
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            await svc.RecordReviewAsync(changeId, Reviewer, ReviewDecision.Approved, "approved", TestContext.CancellationToken);
        }

        // Merge.
        using (var scope = _host.CreateScope())
        {
            var mergeSvc = _host.Resolve<MergeService>(scope);
            var version = await mergeSvc.MergeAsync(changeId, "u", "e@e.com", TestContext.CancellationToken);
            Assert.AreEqual(1, version.SequenceNumber);
        }

        // Verify.
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var loaded = await svc.GetByIdAsync(changeId, TestContext.CancellationToken);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(ChangeStatus.Merged, loaded.Status);
        }

        Assert.IsTrue(await _host.ContentStore.ExistsAsync("detections/ctrl-orch/detection.yaml", TestContext.CancellationToken));
    }

    [TestMethod]
    public void DomainDrivenOrchestrator_IsRegistered()
    {
        using var scope = _host.CreateScope();
        var orchestrator = _host.Resolve<IWorkflowOrchestrator>(scope);
        Assert.IsInstanceOfType<DomainDrivenOrchestrator>(orchestrator);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    public TestContext TestContext { get; set; }
}
