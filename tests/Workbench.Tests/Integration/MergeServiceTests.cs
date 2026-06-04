using Workbench.Application.Abstractions;
using Workbench.Application.Services;

namespace Workbench.Tests.Integration;

[TestClass]
public sealed class MergeServiceTests : IDisposable
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
        return (await svc.ConceiveAsync(slug, "Test Detection", "For merge tests", TestContext.CancellationToken)).Id;
    }

    [TestMethod]
    public async Task QuickLab_Merge_CommitsToContentStore_CreatesVersion_UpdatesDetection()
    {
        var detId = await ConceiveDetection("quick-merge");
        ChangeRequestId changeId;

        // Open and seed a change.
        using (var scope = _host.CreateScope())
        {
            var changeSvc = _host.Resolve<ChangeService>(scope);
            var change = await changeSvc.OpenChangeAsync("CHG-M1", "Initial detection", detId, Author, WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            changeId = change.Id;
            await changeSvc.UpsertDraftFileAsync(changeId, "detection.yaml", DraftContentType.DetectionMetadata, "id: quick-merge", Author, TestContext.CancellationToken);
            await changeSvc.UpsertDraftFileAsync(changeId, "rule.kql", DraftContentType.HuntingQuery, "SigninLogs | take 1", Author, TestContext.CancellationToken);
        }

        // Merge.
        VersionId versionId;
        using (var scope = _host.CreateScope())
        {
            var mergeSvc = _host.Resolve<MergeService>(scope);
            var version = await mergeSvc.MergeAsync(changeId, "Test User", "test@test.com", TestContext.CancellationToken);
            versionId = version.Id;
            Assert.AreEqual(1, version.SequenceNumber);
            Assert.AreEqual("v1", version.DisplayVersion);
            Assert.IsTrue(version.GitCommitSha.StartsWith("fake-sha-", StringComparison.Ordinal));
            Assert.HasCount(2, version.ChangedSections);
        }

        // Verify Git content store.
        Assert.IsTrue(await _host.ContentStore.ExistsAsync("detections/quick-merge/detection.yaml", TestContext.CancellationToken));
        Assert.IsTrue(await _host.ContentStore.ExistsAsync("detections/quick-merge/rule.kql", TestContext.CancellationToken));
        var yamlFile = await _host.ContentStore.GetFileAsync("detections/quick-merge/detection.yaml", TestContext.CancellationToken);
        Assert.AreEqual("id: quick-merge", yamlFile!.Content);

        // Verify change is merged.
        using (var scope = _host.CreateScope())
        {
            var changeSvc = _host.Resolve<ChangeService>(scope);
            var loaded = await changeSvc.GetByIdAsync(changeId, TestContext.CancellationToken);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(ChangeStatus.Merged, loaded.Status);
            Assert.AreEqual(versionId, loaded.ResultVersionId);
        }

        // Verify detection is accepted.
        using (var scope = _host.CreateScope())
        {
            var detSvc = _host.Resolve<DetectionContentService>(scope);
            var det = await detSvc.GetByIdAsync(detId, TestContext.CancellationToken);
            Assert.IsNotNull(det);
            Assert.AreEqual(DetectionLifecycle.Accepted, det.Lifecycle);
            Assert.AreEqual(versionId, det.CurrentVersionId);
        }
    }

    [TestMethod]
    public async Task ControlledReview_Merge_RequiresChecksAndApproval()
    {
        var detId = await ConceiveDetection("ctrl-merge");
        ChangeRequestId changeId;

        using (var scope = _host.CreateScope())
        {
            var changeSvc = _host.Resolve<ChangeService>(scope);
            var change = await changeSvc.OpenChangeAsync("CHG-M2", "Controlled change", detId, Author, WorkflowProfileId.ControlledReview, ct: TestContext.CancellationToken);
            changeId = change.Id;
            await changeSvc.UpsertDraftFileAsync(changeId, "detection.yaml", DraftContentType.DetectionMetadata, "id: ctrl-merge", Author, TestContext.CancellationToken);
        }

        // Merge without checks or reviews — should fail.
        using (var scope = _host.CreateScope())
        {
            var mergeSvc = _host.Resolve<MergeService>(scope);
            var ex = await Assert.ThrowsExactlyAsync<DomainException>(
                () => mergeSvc.MergeAsync(changeId, "Test User", "test@test.com", TestContext.CancellationToken));
            // Should fail on either checks or approval gate.
            Assert.IsTrue(ex.Code.StartsWith("gate.", StringComparison.Ordinal),
                $"Expected a gate error but got '{ex.Code}'.");
        }

        // Add passing checks + non-author approval.
        using (var scope = _host.CreateScope())
        {
            var changeSvc = _host.Resolve<ChangeService>(scope);
            var loaded = await changeSvc.GetByIdAsync(changeId, TestContext.CancellationToken);
            Assert.IsNotNull(loaded);

            var check = loaded.QueueCheck(CheckRunId.New(), "schema-check", isBlocking: true, DateTimeOffset.UtcNow);
            check.MarkRunning(DateTimeOffset.UtcNow);
            check.Complete(CheckStatus.Passed, "ok", "{}", "", DateTimeOffset.UtcNow);
            loaded.AfterCheckPipelineCompleted(DateTimeOffset.UtcNow);

            loaded.RecordReview(ReviewId.New(), Reviewer, ReviewDecision.Approved, "lgtm", DateTimeOffset.UtcNow);

            var uow = _host.Resolve<IUnitOfWork>(scope);
            await uow.SaveChangesAsync(TestContext.CancellationToken);
        }

        // Now merge should succeed.
        using (var scope = _host.CreateScope())
        {
            var mergeSvc = _host.Resolve<MergeService>(scope);
            var version = await mergeSvc.MergeAsync(changeId, "Test User", "test@test.com", TestContext.CancellationToken);
            Assert.AreEqual(1, version.SequenceNumber);
        }
    }

    [TestMethod]
    public async Task Merge_MarksSiblingChangesAsStale()
    {
        var detId = await ConceiveDetection("stale-test");
        ChangeRequestId firstId, secondId;

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var first = await svc.OpenChangeAsync("CHG-S1", "First change", detId, Author, WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            firstId = first.Id;
            await svc.UpsertDraftFileAsync(firstId, "detection.yaml", DraftContentType.DetectionMetadata, "id: stale-test", Author, TestContext.CancellationToken);

            var second = await svc.OpenChangeAsync("CHG-S2", "Second change", detId, Author, WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            secondId = second.Id;
            await svc.UpsertDraftFileAsync(secondId, "rule.kql", DraftContentType.HuntingQuery, "SigninLogs", Author, TestContext.CancellationToken);
        }

        // Merge first.
        using (var scope = _host.CreateScope())
        {
            var mergeSvc = _host.Resolve<MergeService>(scope);
            await mergeSvc.MergeAsync(firstId, "Test User", "test@test.com", TestContext.CancellationToken);
        }

        // Second should be stale.
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var second = await svc.GetByIdAsync(secondId, TestContext.CancellationToken);
            Assert.IsNotNull(second);
            Assert.IsTrue(second.IsStale);
            Assert.IsTrue(second.StaleReason!.Contains("CHG-S1", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public async Task Merge_SecondVersion_IncrementsSequenceNumber()
    {
        var detId = await ConceiveDetection("multi-version");

        // First merge.
        ChangeRequestId firstId;
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var c = await svc.OpenChangeAsync("CHG-V1", "v1 content", detId, Author, WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            firstId = c.Id;
            await svc.UpsertDraftFileAsync(firstId, "detection.yaml", DraftContentType.DetectionMetadata, "v1", Author, TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var mergeSvc = _host.Resolve<MergeService>(scope);
            var v1 = await mergeSvc.MergeAsync(firstId, "u", "e@e.com", TestContext.CancellationToken);
            Assert.AreEqual(1, v1.SequenceNumber);
        }

        // Second merge.
        ChangeRequestId secondId;
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var c = await svc.OpenChangeAsync("CHG-V2", "v2 content", detId, Author, WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            secondId = c.Id;
            await svc.UpsertDraftFileAsync(secondId, "detection.yaml", DraftContentType.DetectionMetadata, "v2", Author, TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var mergeSvc = _host.Resolve<MergeService>(scope);
            var v2 = await mergeSvc.MergeAsync(secondId, "u", "e@e.com", TestContext.CancellationToken);
            Assert.AreEqual(2, v2.SequenceNumber);
            Assert.AreEqual("v2", v2.DisplayVersion);
        }

        // Git should have v2 content at HEAD.
        var yaml = await _host.ContentStore.GetFileAsync("detections/multi-version/detection.yaml", TestContext.CancellationToken);
        Assert.AreEqual("v2", yaml!.Content);
    }

    [TestMethod]
    public async Task Merge_WithInvestigationNote_CommitsNoteToGit()
    {
        var detId = await ConceiveDetection("noted-det");
        ChangeRequestId changeId;

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var c = await svc.OpenChangeAsync("CHG-N1", "Add note", detId, Author, WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            changeId = c.Id;
            await svc.UpsertDraftFileAsync(changeId, "detection.yaml", DraftContentType.DetectionMetadata, "id: noted-det", Author, TestContext.CancellationToken);
            await svc.UpsertDraftFileAsync(changeId, "notes/investigation.md", DraftContentType.InvestigationNote,
                "---\ntags: [T1110]\n---\n\n## Context\n\nBrute force observed.\n", Author, TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var mergeSvc = _host.Resolve<MergeService>(scope);
            await mergeSvc.MergeAsync(changeId, "u", "e@e.com", TestContext.CancellationToken);
        }

        var note = await _host.ContentStore.GetFileAsync("detections/noted-det/notes/investigation.md", TestContext.CancellationToken);
        Assert.IsNotNull(note);
        Assert.IsTrue(note.Content.Contains("T1110", StringComparison.Ordinal));
        Assert.IsFalse(note.IsBinary);
    }

    [TestMethod]
    public async Task Merge_WithStaticAsset_CommitsAsBinary()
    {
        var detId = await ConceiveDetection("asset-det");
        ChangeRequestId changeId;
        var pngB64 = Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A });

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var c = await svc.OpenChangeAsync("CHG-A1", "Add image", detId, Author, WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            changeId = c.Id;
            await svc.UpsertDraftFileAsync(changeId, "detection.yaml", DraftContentType.DetectionMetadata, "id: asset-det", Author, TestContext.CancellationToken);
            await svc.UpsertDraftFileAsync(changeId, "notes/assets/timeline.png", DraftContentType.StaticAsset, pngB64, Author, TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var mergeSvc = _host.Resolve<MergeService>(scope);
            await mergeSvc.MergeAsync(changeId, "u", "e@e.com", TestContext.CancellationToken);
        }

        var asset = await _host.ContentStore.GetFileAsync("detections/asset-det/notes/assets/timeline.png", TestContext.CancellationToken);
        Assert.IsNotNull(asset);
        Assert.IsTrue(asset.IsBinary);
        Assert.AreEqual(pngB64, asset.Content);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    public TestContext TestContext { get; set; }
}