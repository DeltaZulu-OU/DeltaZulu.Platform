using Dapper;
using DeltaZulu.Platform.Application.Workbench.Abstractions;
using DeltaZulu.Platform.Application.Workbench.Services;
using DeltaZulu.Platform.Data.Workbench;

namespace DeltaZulu.Platform.Tests.Workbench.Integration;

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

    private async Task<DetectionId> CreateDetection(string slug = "test-det")
    {
        using var scope = _host.CreateScope();
        var svc = _host.Resolve<DetectionContentService>(scope);
        return (await svc.CreateAsync(slug, "Test Detection", "For merge tests", TestContext.CancellationToken)).Id;
    }

    [TestMethod]
    public async Task QuickLab_Merge_CommitsToContentStore_CreatesVersion_UpdatesDetection()
    {
        var detId = await CreateDetection("quick-merge");
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
        var detId = await CreateDetection("ctrl-merge");
        ChangeRequestId changeId;

        using (var scope = _host.CreateScope())
        {
            var changeSvc = _host.Resolve<ChangeService>(scope);
            var change = await changeSvc.OpenChangeAsync("CHG-M2", "Controlled change", detId, Author, WorkflowProfileId.ControlledReview, ct: TestContext.CancellationToken);
            changeId = change.Id;
            await changeSvc.UpsertDraftFileAsync(changeId, "detection.yaml", DraftContentType.DetectionMetadata,
                "id: ctrl-merge\ntitle: Controlled Merge\ndescription: d\nseverity: high\n", Author, TestContext.CancellationToken);
            await changeSvc.UpsertDraftFileAsync(changeId, "rule.kql", DraftContentType.HuntingQuery,
                "SigninLogs | take 1", Author, TestContext.CancellationToken);
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

            var schemaCheck = loaded.QueueCheck(CheckRunId.New(), "package-schema", isBlocking: true, DateTimeOffset.UtcNow);
            schemaCheck.MarkRunning(DateTimeOffset.UtcNow);
            schemaCheck.Complete(CheckStatus.Passed, "ok", "{}", "", DateTimeOffset.UtcNow);

            var queryCheck = loaded.QueueCheck(CheckRunId.New(), "query-syntax", isBlocking: true, DateTimeOffset.UtcNow);
            queryCheck.MarkRunning(DateTimeOffset.UtcNow);
            queryCheck.Complete(CheckStatus.Passed, "ok", "{}", "", DateTimeOffset.UtcNow);
            loaded.AfterCheckPipelineCompleted(DateTimeOffset.UtcNow);

            loaded.RecordReview(ReviewId.New(), Reviewer, ReviewDecision.Approved, "lgtm", DateTimeOffset.UtcNow);

            var changeRepo = _host.Resolve<IChangeRequestRepository>(scope);
            changeRepo.Save(loaded);

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
        var detId = await CreateDetection("stale-test");
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
        var detId = await CreateDetection("multi-version");

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
        var detId = await CreateDetection("noted-det");
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
        var detId = await CreateDetection("asset-det");
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

    [TestMethod]
    public async Task Merge_PartialDraft_PreservesUnchangedAcceptedFiles()
    {
        var detId = await CreateDetection("partial-preserve");

        // Create accepted v1 with two canonical files.
        ChangeRequestId firstId;
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var c = await svc.OpenChangeAsync("CHG-P1", "initial content", detId, Author,
                WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            firstId = c.Id;
            await svc.UpsertDraftFileAsync(firstId, "detection.yaml", DraftContentType.DetectionMetadata,
                "id: partial-preserve\ntitle: v1", Author, TestContext.CancellationToken);
            await svc.UpsertDraftFileAsync(firstId, "rule.kql", DraftContentType.HuntingQuery,
                "SigninLogs | take 1", Author, TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var mergeSvc = _host.Resolve<MergeService>(scope);
            await mergeSvc.MergeAsync(firstId, "u", "e@e.com", TestContext.CancellationToken);
        }

        // Merge v2 with only metadata in the draft. The existing query must be preserved
        // because draft content is patch-like, not a full package replacement.
        ChangeRequestId secondId;
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var c = await svc.OpenChangeAsync("CHG-P2", "metadata-only edit", detId, Author,
                WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            secondId = c.Id;
            await svc.UpsertDraftFileAsync(secondId, "detection.yaml", DraftContentType.DetectionMetadata,
                "id: partial-preserve\ntitle: v2", Author, TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var mergeSvc = _host.Resolve<MergeService>(scope);
            await mergeSvc.MergeAsync(secondId, "u", "e@e.com", TestContext.CancellationToken);
        }

        var yaml = await _host.ContentStore.GetFileAsync("detections/partial-preserve/detection.yaml", TestContext.CancellationToken);
        var query = await _host.ContentStore.GetFileAsync("detections/partial-preserve/rule.kql", TestContext.CancellationToken);

        Assert.IsNotNull(yaml);
        Assert.AreEqual("id: partial-preserve\ntitle: v2", yaml.Content);
        Assert.IsNotNull(query);
        Assert.AreEqual("SigninLogs | take 1", query.Content);
    }

    [TestMethod]
    public async Task ControlledReview_BlocksMerge_WhenBaseVersionDrifted_WithoutIsStaleFlag()
    {
        var detId = await CreateDetection("stale-drift");

        // Merge a first change to create v1.
        ChangeRequestId firstId;
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var c = await svc.OpenChangeAsync("CHG-D1", "v1 content", detId, Author,
                WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            firstId = c.Id;
            await svc.UpsertDraftFileAsync(firstId, "detection.yaml",
                DraftContentType.DetectionMetadata, "id: stale-drift", Author, TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var mergeSvc = _host.Resolve<MergeService>(scope);
            await mergeSvc.MergeAsync(firstId, "u", "e@e.com", TestContext.CancellationToken);
        }

        // Open a controlled-review change based on v1.
        ChangeRequestId secondId;
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var c = await svc.OpenChangeAsync("CHG-D2", "v2 attempt", detId, Author,
                WorkflowProfileId.ControlledReview, ct: TestContext.CancellationToken);
            secondId = c.Id;
            await svc.UpsertDraftFileAsync(secondId, "detection.yaml",
                DraftContentType.DetectionMetadata, "id: stale-drift-v2", Author, TestContext.CancellationToken);
        }

        // Merge another quick-lab change to advance detection to v2, making the
        // controlled-review change's BaseVersionId stale. The sibling-stale side effect
        // from MergeService will set IsStale on the second change, but the point of this
        // test is that MergeService.MergeAsync also performs its own authoritative check.
        // To prove the authoritative check works independently, we clear IsStale after
        // the third merge.
        ChangeRequestId thirdId;
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var c = await svc.OpenChangeAsync("CHG-D3", "v2 real", detId, Author,
                WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            thirdId = c.Id;
            await svc.UpsertDraftFileAsync(thirdId, "detection.yaml",
                DraftContentType.DetectionMetadata, "id: stale-drift-v2-real", Author, TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var mergeSvc = _host.Resolve<MergeService>(scope);
            await mergeSvc.MergeAsync(thirdId, "u", "e@e.com", TestContext.CancellationToken);
        }

        // Clear the IsStale flag that sibling-stale logic set, to isolate the
        // authoritative base-version comparison in MergeService.
        using (var scope = _host.CreateScope())
        {
            var changeSvc = _host.Resolve<ChangeService>(scope);
            var second = await changeSvc.GetByIdAsync(secondId, TestContext.CancellationToken);
            Assert.IsNotNull(second);
            Assert.IsTrue(second.IsStale, "Sibling stale should have been set by prior merge.");

            // Directly reset the flag via the DB to simulate the flag not being set.
            var uow = _host.Resolve<IUnitOfWork>(scope);
            var changeRepo = _host.Resolve<IChangeRequestRepository>(scope);

            // Reconstitute with IsStale = false by re-opening the change with cleared flag.
            // We use a raw SQL update to bypass domain protection.
            var session = _host.Resolve<DapperSession>(scope);
            await session.Connection.ExecuteAsync(
                "UPDATE change_requests SET is_stale = 0, stale_reason = NULL WHERE id = @Id",
                new { Id = secondId.Value.ToString("D") });
        }

        // Verify IsStale is now false.
        using (var scope = _host.CreateScope())
        {
            var changeSvc = _host.Resolve<ChangeService>(scope);
            var second = await changeSvc.GetByIdAsync(secondId, TestContext.CancellationToken);
            Assert.IsNotNull(second);
            Assert.IsFalse(second.IsStale, "IsStale should have been cleared by direct DB update.");
        }

        // Satisfy controlled-review gates (checks + non-author approval).
        using (var scope = _host.CreateScope())
        {
            var changeSvc = _host.Resolve<ChangeService>(scope);
            var second = await changeSvc.GetByIdAsync(secondId, TestContext.CancellationToken);
            Assert.IsNotNull(second);

            var check = second.QueueCheck(CheckRunId.New(), "test-check", isBlocking: true, DateTimeOffset.UtcNow);
            check.MarkRunning(DateTimeOffset.UtcNow);
            check.Complete(CheckStatus.Passed, "ok", "{}", "", DateTimeOffset.UtcNow);
            second.AfterCheckPipelineCompleted(DateTimeOffset.UtcNow);

            second.RecordReview(ReviewId.New(), Reviewer, ReviewDecision.Approved, "lgtm", DateTimeOffset.UtcNow);

            var changeRepo = _host.Resolve<IChangeRequestRepository>(scope);
            changeRepo.Save(second);

            var uow = _host.Resolve<IUnitOfWork>(scope);
            await uow.SaveChangesAsync(TestContext.CancellationToken);
        }

        // Merge should be blocked by the authoritative base-version comparison,
        // not by the IsStale flag (which we cleared).
        using (var scope = _host.CreateScope())
        {
            var mergeSvc = _host.Resolve<MergeService>(scope);
            var ex = await Assert.ThrowsExactlyAsync<DomainException>(
                () => mergeSvc.MergeAsync(secondId, "u", "e@e.com", TestContext.CancellationToken));
            Assert.AreEqual("gate.stale", ex.Code);
        }
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    public TestContext TestContext { get; set; }
}