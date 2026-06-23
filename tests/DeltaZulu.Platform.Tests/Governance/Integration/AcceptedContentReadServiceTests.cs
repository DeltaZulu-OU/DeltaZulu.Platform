using DeltaZulu.Platform.Application.Governance.Services;

namespace DeltaZulu.Platform.Tests.Governance.Integration;

[TestClass]
public sealed class AcceptedContentReadServiceTests : IDisposable
{
    private static readonly UserId Author = UserId.New();
    private TestServiceProvider _host = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Setup() => _host = new TestServiceProvider();

    [TestCleanup]
    public void Teardown() => _host.Dispose();

    [TestMethod]
    public async Task ListAcceptedFilesForDetectionAsync_ReturnsUiSafeFileSummaries()
    {
        var detectionId = await CreateAcceptedVersionAsync(
            "accepted-read",
            "CHG-READ-1",
            [
                ("detection.yaml", DraftContentType.DetectionMetadata, "id: accepted-read"),
                ("rule.kql", DraftContentType.AnalyticsQuery, "SigninLogs | take 1"),
            ]);

        using var scope = _host.CreateScope();
        var service = _host.Resolve<AcceptedContentReadService>(scope);

        var files = await service.ListAcceptedFilesForDetectionAsync(detectionId, TestContext.CancellationToken);

        Assert.HasCount(2, files);
        Assert.AreEqual("detection.yaml", files[0].LogicalPath);
        Assert.AreEqual("detections/accepted-read.yaml", files[0].RepositoryPath);
        Assert.AreEqual("rule.kql", files[1].LogicalPath);
        Assert.AreEqual("detections/accepted-read-rule.kql", files[1].RepositoryPath);
    }

    [TestMethod]
    public async Task CompareDraftToAcceptedAsync_ReturnsNewModifiedAndUnchangedReadModels()
    {
        var detectionId = await CreateAcceptedVersionAsync(
            "draft-compare",
            "CHG-CMP-1",
            [
                ("detection.yaml", DraftContentType.DetectionMetadata, "id: draft-compare\ntitle: v1"),
                ("rule.kql", DraftContentType.AnalyticsQuery, "SigninLogs | take 1"),
            ]);

        ChangeRequestId changeId;
        using (var scope = _host.CreateScope())
        {
            var changes = _host.Resolve<ChangeService>(scope);
            var change = await changes.OpenChangeAsync(
                "CHG-CMP-2",
                "Draft comparison",
                detectionId,
                Author,
                WorkflowProfileId.QuickLab,
                ct: TestContext.CancellationToken);
            changeId = change.Id;

            await changes.UpsertDraftFileAsync(
                changeId,
                "detection.yaml",
                DraftContentType.DetectionMetadata,
                "id: draft-compare\ntitle: v2",
                Author,
                TestContext.CancellationToken);
            await changes.UpsertDraftFileAsync(
                changeId,
                "rule.kql",
                DraftContentType.AnalyticsQuery,
                "SigninLogs | take 1",
                Author,
                TestContext.CancellationToken);
            await changes.UpsertDraftFileAsync(
                changeId,
                "tests/baseline.yaml",
                DraftContentType.TestDefinition,
                "expectedRows: 1",
                Author,
                TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var service = _host.Resolve<AcceptedContentReadService>(scope);
            var comparisons = await service.CompareDraftToAcceptedAsync(changeId, TestContext.CancellationToken);

            AssertStatus(comparisons, "detection.yaml", DraftAcceptedComparisonStatus.Modified);
            AssertStatus(comparisons, "rule.kql", DraftAcceptedComparisonStatus.Unchanged);
            AssertStatus(comparisons, "tests/baseline.yaml", DraftAcceptedComparisonStatus.New);
        }
    }

    private async Task<DetectionId> CreateAcceptedVersionAsync(
        string slug,
        string changeKey,
        IReadOnlyList<(string Path, DraftContentType ContentType, string Content)> files)
    {
        using var scope = _host.CreateScope();
        var detections = _host.Resolve<DetectionContentService>(scope);
        var changes = _host.Resolve<ChangeService>(scope);
        var merge = _host.Resolve<MergeService>(scope);

        var detection = await detections.CreateAsync(
            slug,
            slug,
            "For accepted-content read service tests",
            TestContext.CancellationToken);
        var change = await changes.OpenChangeAsync(
            changeKey,
            "Initial content",
            detection.Id,
            Author,
            WorkflowProfileId.QuickLab,
            ct: TestContext.CancellationToken);

        foreach (var file in files)
        {
            await changes.UpsertDraftFileAsync(
                change.Id,
                file.Path,
                file.ContentType,
                file.Content,
                Author,
                TestContext.CancellationToken);
        }

        await merge.MergeAsync(change.Id, "Test Author", "test@example.invalid", TestContext.CancellationToken);
        return detection.Id;
    }

    private static void AssertStatus(
        IReadOnlyList<DraftAcceptedComparison> comparisons,
        string logicalPath,
        DraftAcceptedComparisonStatus expected)
    {
        var comparison = comparisons.Single(file => file.LogicalPath == logicalPath);
        Assert.AreEqual(expected, comparison.Status);
    }

    public void Dispose() => _host.Dispose();
}