using DeltaZulu.Platform.Application.Governance.Services;
using DeltaZulu.Platform.Domain.Analytics.Detections;
using DeltaZulu.Platform.Domain.Governance.Contracts;
using DeltaZulu.Platform.Domain.Governance.Detections;

namespace DeltaZulu.Platform.Tests.Governance.Integration;

/// <summary>
/// Covers the Phase 4 remaining scope: projection diagnostics for invalid/incomplete accepted
/// content, idempotent replay of the same accepted version, and the backfill command for
/// content accepted before (or despite) the projection pipeline.
/// </summary>
[TestClass]
public sealed class DetectionProjectionTests : IDisposable
{
    private TestServiceProvider _host = null!;
    private static readonly UserId Author = UserId.New();

    [TestInitialize]
    public void Setup() => _host = new TestServiceProvider();

    [TestCleanup]
    public void Teardown() => _host.Dispose();

    private async Task<DetectionId> CreateDetection(string slug)
    {
        using var scope = _host.CreateScope();
        var svc = _host.Resolve<DetectionContentService>(scope);
        return (await svc.CreateAsync(slug, "Test Detection", "For projection tests", TestContext.CancellationToken)).Id;
    }

    private async Task<ChangeRequestId> OpenAndSeedChange(
        DetectionId detId, string changeKey, string yaml, string? query = "SigninLogs | take 1")
    {
        using var scope = _host.CreateScope();
        var changeSvc = _host.Resolve<ChangeService>(scope);
        var change = await changeSvc.OpenChangeAsync(
            changeKey, "Projection test change", detId, Author, WorkflowProfileId.QuickLab, ct: TestContext.CancellationToken);
        await changeSvc.UpsertDraftFileAsync(
            change.Id, "detection.yaml", DraftContentType.DetectionMetadata, yaml, Author, TestContext.CancellationToken);
        if (query is not null)
        {
            await changeSvc.UpsertDraftFileAsync(
                change.Id, "rule.kql", DraftContentType.AnalyticsQuery, query, Author, TestContext.CancellationToken);
        }

        return change.Id;
    }

    [TestMethod]
    public async Task Merge_WithQueryField_ProjectsExecutableRecord()
    {
        var detId = await CreateDetection("proj-ok");
        var changeId = await OpenAndSeedChange(
            detId, "CHG-PJ1", "id: proj-ok\ntitle: Proj Ok\nquery: SigninLogs | take 1\nseverity: high\n");

        using var scope = _host.CreateScope();
        var mergeSvc = _host.Resolve<MergeService>(scope);
        var version = await mergeSvc.MergeAsync(changeId, "u", "e@e.com", TestContext.CancellationToken);

        var expectedId = $"{detId}-{version.Id}";
        var records = _host.Resolve<IDetectionRecordRepository>(scope);
        var record = await records.GetAsync(expectedId, TestContext.CancellationToken);

        Assert.IsNotNull(record);
        Assert.AreEqual("SigninLogs | take 1", record.QueryText);
        Assert.AreEqual(version.Id.ToString(), record.AcceptedVersionId);

        var diagnostics = _host.Resolve<IDetectionProjectionDiagnosticRepository>(scope);
        Assert.HasCount(0, await diagnostics.ListByDetectionAsync(detId.ToString(), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Merge_WithoutQueryField_RecordsMissingQueryDiagnostic()
    {
        var detId = await CreateDetection("proj-noquery");
        var changeId = await OpenAndSeedChange(
            detId, "CHG-PJ2", "id: proj-noquery\ntitle: No Query\n", query: null);

        using var scope = _host.CreateScope();
        var mergeSvc = _host.Resolve<MergeService>(scope);
        var version = await mergeSvc.MergeAsync(changeId, "u", "e@e.com", TestContext.CancellationToken);

        var expectedId = $"{detId}-{version.Id}";
        var records = _host.Resolve<IDetectionRecordRepository>(scope);
        Assert.IsNull(await records.GetAsync(expectedId, TestContext.CancellationToken));

        var diagnostics = _host.Resolve<IDetectionProjectionDiagnosticRepository>(scope);
        var found = await diagnostics.ListByDetectionAsync(detId.ToString(), TestContext.CancellationToken);
        Assert.HasCount(1, found);
        Assert.AreEqual(DetectionProjectionDiagnosticReason.MissingQuery, found[0].Reason);
        Assert.AreEqual(expectedId, found[0].Id);
    }

    [TestMethod]
    public async Task Merge_WithUnparsableMetadata_RecordsMetadataUnreadableDiagnostic()
    {
        var detId = await CreateDetection("proj-badyaml");
        // Invalid YAML (unterminated flow mapping) so TryReadMetadata fails to parse.
        var changeId = await OpenAndSeedChange(detId, "CHG-PJ3", "id: [unterminated", query: null);

        using var scope = _host.CreateScope();
        var mergeSvc = _host.Resolve<MergeService>(scope);
        var version = await mergeSvc.MergeAsync(changeId, "u", "e@e.com", TestContext.CancellationToken);

        var diagnostics = _host.Resolve<IDetectionProjectionDiagnosticRepository>(scope);
        var found = await diagnostics.ListByDetectionAsync(detId.ToString(), TestContext.CancellationToken);
        Assert.HasCount(1, found);
        Assert.AreEqual(DetectionProjectionDiagnosticReason.MetadataUnreadable, found[0].Reason);
        Assert.AreEqual($"{detId}-{version.Id}", found[0].Id);
    }

    [TestMethod]
    public async Task Merge_ThenFixedFollowUpVersion_LeavesFirstDiagnosticAndAddsCleanSecondProjection()
    {
        var detId = await CreateDetection("proj-fix");
        var firstChange = await OpenAndSeedChange(
            detId, "CHG-PJ4", "id: proj-fix\ntitle: v1\n", query: null);

        DetectionVersion v1;
        using (var scope = _host.CreateScope())
        {
            var mergeSvc = _host.Resolve<MergeService>(scope);
            v1 = await mergeSvc.MergeAsync(firstChange, "u", "e@e.com", TestContext.CancellationToken);
        }

        var firstDiagnosticId = $"{detId}-{v1.Id}";
        using (var scope = _host.CreateScope())
        {
            var diagnostics = _host.Resolve<IDetectionProjectionDiagnosticRepository>(scope);
            var all = await diagnostics.ListByDetectionAsync(detId.ToString(), TestContext.CancellationToken);
            Assert.HasCount(1, all);
            Assert.AreEqual(firstDiagnosticId, all[0].Id);
        }

        var secondChange = await OpenAndSeedChange(
            detId, "CHG-PJ5", "id: proj-fix\ntitle: v2\nquery: SigninLogs | take 1\n");

        DetectionVersion v2;
        using (var scope = _host.CreateScope())
        {
            var mergeSvc = _host.Resolve<MergeService>(scope);
            v2 = await mergeSvc.MergeAsync(secondChange, "u", "e@e.com", TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var diagnostics = _host.Resolve<IDetectionProjectionDiagnosticRepository>(scope);
            // v1's diagnostic remains as an accurate historical record for that specific
            // accepted version; v2 projects cleanly and does not add a new diagnostic.
            var all = await diagnostics.ListByDetectionAsync(detId.ToString(), TestContext.CancellationToken);
            Assert.HasCount(1, all);
            Assert.AreEqual(firstDiagnosticId, all[0].Id);

            var records = _host.Resolve<IDetectionRecordRepository>(scope);
            Assert.IsNotNull(await records.GetAsync($"{detId}-{v2.Id}", TestContext.CancellationToken));
        }
    }

    [TestMethod]
    public async Task ProjectAsync_ReplayedForSameAcceptedVersion_IsIdempotent()
    {
        var detId = await CreateDetection("proj-idem");
        var changeId = await OpenAndSeedChange(
            detId, "CHG-PJ6", "id: proj-idem\ntitle: Idem\nquery: SigninLogs | take 1\n");

        using var scope = _host.CreateScope();
        var mergeSvc = _host.Resolve<MergeService>(scope);
        var version = await mergeSvc.MergeAsync(changeId, "u", "e@e.com", TestContext.CancellationToken);

        var detections = _host.Resolve<IDetectionRepository>(scope);
        var detection = await detections.GetByIdAsync(detId, TestContext.CancellationToken);
        Assert.IsNotNull(detection);

        var projections = _host.Resolve<IDetectionProjectionService>(scope);
        var first = await projections.ProjectAsync(detection, version, TestContext.CancellationToken);
        var second = await projections.ProjectAsync(detection, version, TestContext.CancellationToken);

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.AreEqual(first.Id, second.Id);
        Assert.AreEqual(first.RuleHash, second.RuleHash);

        var records = _host.Resolve<IDetectionRecordRepository>(scope);
        var versions = await records.ListVersionsAsync(detId.ToString(), TestContext.CancellationToken);
        Assert.HasCount(1, versions);
    }

    [TestMethod]
    public async Task Backfill_ProjectsMissingRecord_SkipsAlreadyProjected_CountsUnaccepted()
    {
        // Detection A: merged and projected normally -> should be reported as already projected.
        var detA = await CreateDetection("bf-a");
        var changeA = await OpenAndSeedChange(
            detA, "CHG-BFA", "id: bf-a\ntitle: A\nquery: SigninLogs | take 1\n");

        // Detection B: merged, but its executable record is deleted afterward to simulate
        // content accepted before the projection pipeline existed.
        var detB = await CreateDetection("bf-b");
        var changeB = await OpenAndSeedChange(
            detB, "CHG-BFB", "id: bf-b\ntitle: B\nquery: SigninLogs | take 1\n");

        // Detection C: never merged -> not yet accepted.
        var detC = await CreateDetection("bf-c");

        DetectionVersion versionA, versionB;
        using (var scope = _host.CreateScope())
        {
            var mergeSvc = _host.Resolve<MergeService>(scope);
            versionA = await mergeSvc.MergeAsync(changeA, "u", "e@e.com", TestContext.CancellationToken);
            versionB = await mergeSvc.MergeAsync(changeB, "u", "e@e.com", TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var records = _host.Resolve<IDetectionRecordRepository>(scope);
            await records.DeleteAsync($"{detB}-{versionB.Id}", TestContext.CancellationToken);
        }

        using var finalScope = _host.CreateScope();
        var backfill = _host.Resolve<DetectionProjectionBackfillService>(finalScope);
        var result = await backfill.BackfillAsync(TestContext.CancellationToken);

        Assert.AreEqual(3, result.TotalDetections);
        Assert.AreEqual(1, result.AlreadyProjected, "Detection A was already projected.");
        Assert.AreEqual(1, result.Projected, "Detection B's missing record should be re-projected.");
        Assert.AreEqual(1, result.NotAccepted, "Detection C has no accepted version.");
        Assert.AreEqual(0, result.Failed);

        var records2 = _host.Resolve<IDetectionRecordRepository>(finalScope);
        Assert.IsNotNull(await records2.GetAsync($"{detB}-{versionB.Id}", TestContext.CancellationToken));
    }

    public void Dispose() => _host.Dispose();

    public TestContext TestContext { get; set; }
}
