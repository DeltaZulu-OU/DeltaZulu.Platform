using DeltaZulu.Platform.Application.Governance.Services;

namespace DeltaZulu.Platform.Tests.Governance.Integration;

[TestClass]
public sealed class CheckPipelineRunnerTests : IDisposable
{
    private TestServiceProvider _host = null!;
    private static readonly UserId Author = UserId.New();

    [TestInitialize]
    public void Setup() => _host = new TestServiceProvider();

    [TestCleanup]
    public void Teardown() => _host.Dispose();

    private async Task<DetectionId> CreateDetection(string slug = "check-det")
    {
        using var scope = _host.CreateScope();
        var svc = _host.Resolve<DetectionContentService>(scope);
        return (await svc.CreateAsync(slug, "Check Test Detection", "", TestContext.CancellationToken)).Id;
    }

    [TestMethod]
    public async Task Pipeline_ValidPackage_AllBlockingChecksPassed_ChangeAdvancesToReviewRequired()
    {
        var detId = await CreateDetection();
        ChangeRequestId changeId;

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var c = await svc.OpenChangeAsync("CHG-P1", "Full package", detId, Author, WorkflowProfileId.ControlledReview, ct: TestContext.CancellationToken);
            changeId = c.Id;
            await svc.UpsertDraftFileAsync(changeId, "detection.yaml", DraftContentType.DetectionMetadata,
                "id: check-det\ntitle: Check Test\ndescription: Tests checks\nseverity: high\n", Author, TestContext.CancellationToken);
            await svc.UpsertDraftFileAsync(changeId, "rule.kql", DraftContentType.AnalyticsQuery,
                "SigninLogs | where ResultType != 0", Author, TestContext.CancellationToken);
        }

        // Run pipeline.
        using (var scope = _host.CreateScope())
        {
            var runner = _host.Resolve<CheckPipelineRunner>(scope);
            var results = await runner.RunAsync(changeId, TestContext.CancellationToken);

            // Should have run at least package-schema and query-syntax.
            Assert.IsGreaterThanOrEqualTo(2, results.Count,
                $"Expected at least 2 checks to run, got {results.Count}: {string.Join(", ", results.Select(r => r.CheckName))}");

            Assert.IsTrue(results.All(r => r.Outcome.Status == CheckStatus.Passed),
                $"All checks should pass, but: {string.Join(", ", results.Where(r => r.Outcome.Status != CheckStatus.Passed).Select(r => $"{r.CheckName}={r.Outcome.Status}"))}");
        }

        // Change should have advanced.
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var loaded = await svc.GetByIdAsync(changeId, TestContext.CancellationToken);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(ChangeStatus.ReviewRequired, loaded.Status,
                "After passing checks on ControlledReview, status should be ReviewRequired.");
            Assert.IsGreaterThanOrEqualTo(2, loaded.Checks.Count);
            Assert.IsTrue(loaded.Checks.All(c => c.Status == CheckStatus.Passed));
        }
    }

    [TestMethod]
    public async Task Pipeline_InvalidSchema_BlockingCheckFails_ChangeRevertsToDraft()
    {
        var detId = await CreateDetection("fail-det");
        ChangeRequestId changeId;

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var c = await svc.OpenChangeAsync("CHG-P2", "Bad schema", detId, Author, WorkflowProfileId.ControlledReview, ct: TestContext.CancellationToken);
            changeId = c.Id;
            // Missing required fields (description, severity).
            await svc.UpsertDraftFileAsync(changeId, "detection.yaml", DraftContentType.DetectionMetadata,
                "id: fail-det\ntitle: Incomplete\n", Author, TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var runner = _host.Resolve<CheckPipelineRunner>(scope);
            var results = await runner.RunAsync(changeId, TestContext.CancellationToken);

            var schemaResult = results.FirstOrDefault(r => r.CheckName == "package-schema");
            Assert.IsNotNull(schemaResult);
            Assert.AreEqual(CheckStatus.Failed, schemaResult.Outcome.Status);
        }

        // Change should revert to Draft (blocking check failed under ControlledReview).
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var loaded = await svc.GetByIdAsync(changeId, TestContext.CancellationToken);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(ChangeStatus.Draft, loaded.Status);
        }
    }

    [TestMethod]
    public async Task Pipeline_ControlledReview_MissingRequiredQueryCheck_RemainsDraft()
    {
        var detId = await CreateDetection("metadata-only-det");
        ChangeRequestId changeId;

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var c = await svc.OpenChangeAsync("CHG-MISS", "Metadata only", detId, Author,
                WorkflowProfileId.ControlledReview, ct: TestContext.CancellationToken);
            changeId = c.Id;
            await svc.UpsertDraftFileAsync(changeId, "detection.yaml", DraftContentType.DetectionMetadata,
                "id: metadata-only-det\ntitle: Metadata Only\ndescription: Missing query\nseverity: low\n",
                Author, TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var runner = _host.Resolve<CheckPipelineRunner>(scope);
            var results = await runner.RunAsync(changeId, TestContext.CancellationToken);

            Assert.Contains(r => r.CheckName == "package-schema" && r.Outcome.Status == CheckStatus.Passed, results);
            Assert.DoesNotContain(r => r.CheckName == "query-syntax", results,
                "The query check should be skipped because no query file is present.");
        }

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var loaded = await svc.GetByIdAsync(changeId, TestContext.CancellationToken);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(ChangeStatus.Draft, loaded.Status,
                "ControlledReview must not advance when a configured required check was skipped.");
            Assert.Contains(g => g.Code == "gate.checks_missing", loaded.EvaluateMergeReadiness().UnmetGates);
        }
    }

    [TestMethod]
    public async Task Pipeline_QuickLab_FailedCheckDoesNotBlock_ChangeAdvancesToReadyToAccept()
    {
        var detId = await CreateDetection("ql-fail-det");
        ChangeRequestId changeId;

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var c = await svc.OpenChangeAsync("CHG-P3", "QuickLab bad schema", detId, Author, WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            changeId = c.Id;
            await svc.UpsertDraftFileAsync(changeId, "detection.yaml", DraftContentType.DetectionMetadata,
                "id: ql-fail-det\n", Author, TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var runner = _host.Resolve<CheckPipelineRunner>(scope);
            await runner.RunAsync(changeId, TestContext.CancellationToken);
        }

        // QuickLab doesn't require passing checks, so status should advance to ReadyToAccept.
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var loaded = await svc.GetByIdAsync(changeId, TestContext.CancellationToken);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(ChangeStatus.ReadyToAccept, loaded.Status);
        }
    }

    [TestMethod]
    public async Task Pipeline_WithInvestigationNote_RunsNonBlockingNoteCheck()
    {
        var detId = await CreateDetection("note-check-det");
        ChangeRequestId changeId;

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var c = await svc.OpenChangeAsync("CHG-P4", "With note", detId, Author, WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            changeId = c.Id;
            await svc.UpsertDraftFileAsync(changeId, "detection.yaml", DraftContentType.DetectionMetadata,
                "id: note-check-det\ntitle: t\ndescription: d\nseverity: low\n", Author, TestContext.CancellationToken);
            await svc.UpsertDraftFileAsync(changeId, "notes/investigation.md", DraftContentType.InvestigationNote,
                "---\ntags: [T1110]\nobservables:\n  - type: ip\n    value: 10.0.0.1\n---\n\n## Context\n", Author, TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var runner = _host.Resolve<CheckPipelineRunner>(scope);
            var results = await runner.RunAsync(changeId, TestContext.CancellationToken);

            var noteResult = results.FirstOrDefault(r => r.CheckName == "note-frontmatter");
            Assert.IsNotNull(noteResult, "Note frontmatter check should have run.");
            Assert.AreEqual(CheckStatus.Passed, noteResult.Outcome.Status);
        }

        // Verify the check run was persisted on the change.
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var loaded = await svc.GetByIdAsync(changeId, TestContext.CancellationToken);
            Assert.IsNotNull(loaded);
            Assert.Contains(c => c.Name == "note-frontmatter", loaded.Checks);
            var noteCheck = loaded.Checks.First(c => c.Name == "note-frontmatter");
            Assert.IsFalse(noteCheck.IsBlocking, "Note frontmatter check should be non-blocking.");
        }
    }

    [TestMethod]
    public async Task Pipeline_WithFixtures_RunsFixtureCheck()
    {
        var detId = await CreateDetection("fixture-check-det");
        ChangeRequestId changeId;

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var c = await svc.OpenChangeAsync("CHG-P5", "With fixtures", detId, Author, WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            changeId = c.Id;
            await svc.UpsertDraftFileAsync(changeId, "detection.yaml", DraftContentType.DetectionMetadata,
                "id: fixture-check-det\ntitle: t\ndescription: d\nseverity: low\n", Author, TestContext.CancellationToken);
            await svc.UpsertDraftFileAsync(changeId, "fixtures/sign-in.ndjson", DraftContentType.Fixture,
                "{\"user\":\"admin\",\"result\":0}\n{\"user\":\"guest\",\"result\":50074}\n", Author, TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var runner = _host.Resolve<CheckPipelineRunner>(scope);
            var results = await runner.RunAsync(changeId, TestContext.CancellationToken);

            var fixtureResult = results.FirstOrDefault(r => r.CheckName == "fixture-parse");
            Assert.IsNotNull(fixtureResult);
            Assert.AreEqual(CheckStatus.Passed, fixtureResult.Outcome.Status);
        }
    }

    [TestMethod]
    public async Task Pipeline_StaticAssetOnly_SkipsAllTextChecks()
    {
        var detId = await CreateDetection("asset-only-det");
        ChangeRequestId changeId;

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var c = await svc.OpenChangeAsync("CHG-P6", "Asset only", detId, Author, WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
            changeId = c.Id;
            var b64 = Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
            await svc.UpsertDraftFileAsync(changeId, "notes/assets/img.png", DraftContentType.StaticAsset, b64, Author, TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var runner = _host.Resolve<CheckPipelineRunner>(scope);
            var results = await runner.RunAsync(changeId, TestContext.CancellationToken);

            // No checks should have run — no applicable content types for any registered check.
            Assert.IsEmpty(results,
                $"Expected 0 checks for asset-only change, got: {string.Join(", ", results.Select(r => r.CheckName))}");
        }
    }

    [TestMethod]
    public async Task Pipeline_ReRun_ClearsOldChecks_SecondRunNotPollutedByFirst()
    {
        var detId = await CreateDetection("rerun-det");
        ChangeRequestId changeId;

        // Create change with invalid schema (missing fields).
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var c = await svc.OpenChangeAsync("CHG-RR", "Rerun test", detId, Author, WorkflowProfileId.ControlledReview, ct: TestContext.CancellationToken);
            changeId = c.Id;
            await svc.UpsertDraftFileAsync(changeId, "detection.yaml", DraftContentType.DetectionMetadata,
                "id: rerun-det\ntitle: Incomplete\n", Author, TestContext.CancellationToken);
        }

        // First run: schema check fails.
        using (var scope = _host.CreateScope())
        {
            var runner = _host.Resolve<CheckPipelineRunner>(scope);
            var results = await runner.RunAsync(changeId, TestContext.CancellationToken);
            Assert.Contains(r => r.CheckName == "package-schema" && r.Outcome.Status == CheckStatus.Failed, results);
        }

        // Fix the content.
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            await svc.UpsertDraftFileAsync(changeId, "detection.yaml", DraftContentType.DetectionMetadata,
                "id: rerun-det\ntitle: Fixed\ndescription: Now complete\nseverity: low\n", Author, TestContext.CancellationToken);
            await svc.UpsertDraftFileAsync(changeId, "rule.kql", DraftContentType.AnalyticsQuery,
                "SigninLogs | take 1", Author, TestContext.CancellationToken);
        }

        // Second run: should pass — old failed check must not block.
        using (var scope = _host.CreateScope())
        {
            var runner = _host.Resolve<CheckPipelineRunner>(scope);
            var results = await runner.RunAsync(changeId, TestContext.CancellationToken);
            Assert.IsTrue(results.All(r => r.Outcome.Status == CheckStatus.Passed),
                "After fixing content and re-running, all checks should pass.");
        }

        // Verify: only the second run's checks exist on the change.
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<ChangeService>(scope);
            var loaded = await svc.GetByIdAsync(changeId, TestContext.CancellationToken);
            Assert.IsNotNull(loaded);
            Assert.IsTrue(loaded.Checks.All(c => c.Status == CheckStatus.Passed),
                "No stale failed checks from the first run should remain.");
            Assert.AreEqual(ChangeStatus.ReviewRequired, loaded.Status,
                "After passing checks on ControlledReview, status should be ReviewRequired.");
        }
    }

    public void Dispose() => _host.Dispose();

    public TestContext TestContext { get; set; }
}