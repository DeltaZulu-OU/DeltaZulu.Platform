using Workbench.Application.Services;
using Workbench.Domain.Changes;
using Workbench.Domain.Detections;

namespace Workbench.Tests.Integration;

[TestClass]
public sealed class RestoreServiceTests : IDisposable
{
    private TestServiceProvider _host = null!;
    private static readonly UserId Author = UserId.New();

    [TestInitialize]
    public void Setup() => _host = new TestServiceProvider();

    [TestCleanup]
    public void Teardown() => _host.Dispose();

    [TestMethod]
    public async Task RestoreVersionAsChange_PopulatesDraftFromAcceptedVersion_WithoutRewritingHistory()
    {
        var detectionId = await ConceiveDetectionAsync("restore-det");
        var version1 = await MergeQuickLabAsync(
            detectionId,
            "CHG-R1",
            "Initial accepted content",
            new[]
            {
                ("detection.yaml", DraftContentType.DetectionMetadata, "id: restore-det\ntitle: v1"),
                ("rule.kql", DraftContentType.HuntingQuery, "SigninLogs | take 1"),
                ("tests/baseline.yaml", DraftContentType.TestDefinition, "expectedRows: 1"),
            });

        var version2 = await MergeQuickLabAsync(
            detectionId,
            "CHG-R2",
            "Bad edit",
            new[]
            {
                ("detection.yaml", DraftContentType.DetectionMetadata, "id: restore-det\ntitle: v2"),
                ("rule.kql", DraftContentType.HuntingQuery, "SigninLogs | take 2"),
            });

        ChangeRequestId restoreChangeId;
        using (var scope = _host.CreateScope())
        {
            var restoreSvc = _host.Resolve<RestoreService>(scope);
            var restore = await restoreSvc.RestoreVersionAsChangeAsync(
                version1.Id,
                Author,
                WorkflowProfileId.QuickLab,
                key: "RST-R1",
                title: "Restore known-good content",
                ct: TestContext.CancellationToken);

            restoreChangeId = restore.Id;
            Assert.AreEqual(detectionId, restore.DetectionId);
            Assert.AreEqual(version2.Id, restore.BaseVersionId);
            Assert.AreEqual(ChangeStatus.Draft, restore.Status);
            Assert.HasCount(3, restore.DraftFiles);
            Assert.AreEqual("id: restore-det\ntitle: v1", DraftContent(restore, "detection.yaml"));
            Assert.AreEqual("SigninLogs | take 1", DraftContent(restore, "rule.kql"));
            Assert.AreEqual("expectedRows: 1", DraftContent(restore, "tests/baseline.yaml"));
        }

        Assert.AreEqual(version2.GitCommitSha, await _host.ContentStore.GetHeadCommitShaAsync(TestContext.CancellationToken));

        using (var scope = _host.CreateScope())
        {
            var changeSvc = _host.Resolve<ChangeService>(scope);
            var persisted = await changeSvc.GetByIdAsync(restoreChangeId, TestContext.CancellationToken);
            Assert.IsNotNull(persisted);
            Assert.HasCount(3, persisted.DraftFiles);
        }
    }

    [TestMethod]
    public async Task RestoreVersionAsChange_PreservesBinaryAcceptedFilesAsStaticAssets()
    {
        var detectionId = await ConceiveDetectionAsync("restore-binary");
        var pngB64 = Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        var version = await MergeQuickLabAsync(
            detectionId,
            "CHG-B1",
            "Accepted image",
            new[]
            {
                ("detection.yaml", DraftContentType.DetectionMetadata, "id: restore-binary"),
                ("notes/assets/timeline.png", DraftContentType.StaticAsset, pngB64),
            });

        using var scope = _host.CreateScope();
        var restoreSvc = _host.Resolve<RestoreService>(scope);
        var restore = await restoreSvc.RestoreVersionAsChangeAsync(
            version.Id,
            Author,
            WorkflowProfileId.QuickLab,
            key: "RST-B1",
            ct: TestContext.CancellationToken);

        var asset = restore.DraftFiles.Single(f => f.Path.Value == "notes/assets/timeline.png");
        Assert.AreEqual(DraftContentType.StaticAsset, asset.ContentType);
        Assert.AreEqual(pngB64, asset.Content);
    }

    private async Task<DetectionId> ConceiveDetectionAsync(string slug)
    {
        using var scope = _host.CreateScope();
        var svc = _host.Resolve<DetectionContentService>(scope);
        return (await svc.ConceiveAsync(slug, "Restore Test Detection", "", TestContext.CancellationToken)).Id;
    }

    private async Task<DetectionVersion> MergeQuickLabAsync(
        DetectionId detectionId,
        string key,
        string title,
        IReadOnlyList<(string Path, DraftContentType Type, string Content)> files)
    {
        ChangeRequestId changeId;
        using (var scope = _host.CreateScope())
        {
            var changeSvc = _host.Resolve<ChangeService>(scope);
            var change = await changeSvc.OpenChangeAsync(
                key,
                title,
                detectionId,
                Author,
                WorkflowProfileId.QuickLab,
                ct: TestContext.CancellationToken);
            changeId = change.Id;

            foreach (var file in files)
            {
                await changeSvc.UpsertDraftFileAsync(
                    changeId,
                    file.Path,
                    file.Type,
                    file.Content,
                    Author,
                    TestContext.CancellationToken);
            }
        }

        using (var scope = _host.CreateScope())
        {
            var mergeSvc = _host.Resolve<MergeService>(scope);
            return await mergeSvc.MergeAsync(changeId, "Restore Tester", "restore@example.test", TestContext.CancellationToken);
        }
    }

    private static string DraftContent(ChangeRequest change, string logicalPath)
        => change.DraftFiles.Single(f => f.Path.Value == logicalPath).Content;

    public void Dispose()
    {
        _host.Dispose();
    }

    public TestContext TestContext { get; set; }
}
