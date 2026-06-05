using Workbench.Application.Services;
using Workbench.Domain.Changes;
using Workbench.Domain.Detections;

namespace Workbench.Tests.Integration;

[TestClass]
public sealed class VersionServiceTests : IDisposable
{
    private TestServiceProvider _host = null!;
    private static readonly UserId Author = UserId.New();

    [TestInitialize]
    public void Setup() => _host = new TestServiceProvider();

    [TestCleanup]
    public void Teardown() => _host.Dispose();

    [TestMethod]
    public async Task CompareWithPrevious_FirstVersion_ReportsAcceptedFilesAsAdded()
    {
        var detectionId = await ConceiveDetectionAsync("compare-first");
        var version = await MergeQuickLabAsync(
            detectionId,
            "CHG-C1",
            "Initial accepted content",
            new[]
            {
                ("detection.yaml", DraftContentType.DetectionMetadata, "id: compare-first"),
                ("rule.kql", DraftContentType.HuntingQuery, "SigninLogs | take 1"),
            });

        using var scope = _host.CreateScope();
        var versionSvc = _host.Resolve<VersionService>(scope);
        var comparison = await versionSvc.CompareWithPreviousAsync(version.Id, TestContext.CancellationToken);

        Assert.IsNull(comparison.BeforeVersion);
        Assert.AreEqual(version.Id, comparison.AfterVersion.Id);
        Assert.HasCount(2, comparison.ChangedFiles);
        Assert.IsTrue(comparison.Files.All(file => file.Status == VersionFileDiffStatus.Added));
    }

    [TestMethod]
    public async Task CompareWithPrevious_ReportsAddedModifiedAndUnchangedFiles()
    {
        var detectionId = await ConceiveDetectionAsync("compare-changes");
        await MergeQuickLabAsync(
            detectionId,
            "CHG-C2A",
            "Initial accepted content",
            new[]
            {
                ("detection.yaml", DraftContentType.DetectionMetadata, "id: compare-changes\ntitle: v1"),
                ("rule.kql", DraftContentType.HuntingQuery, "SigninLogs | take 1"),
                ("fixtures/same.ndjson", DraftContentType.Fixture, "{\"id\":1}"),
            });
        var version2 = await MergeQuickLabAsync(
            detectionId,
            "CHG-C2B",
            "Second accepted content",
            new[]
            {
                ("detection.yaml", DraftContentType.DetectionMetadata, "id: compare-changes\ntitle: v2"),
                ("rule.kql", DraftContentType.HuntingQuery, "SigninLogs | take 1"),
                ("fixtures/same.ndjson", DraftContentType.Fixture, "{\"id\":1}"),
                ("tests/added.yaml", DraftContentType.TestDefinition, "expectedRows: 2"),
            });

        using var scope = _host.CreateScope();
        var versionSvc = _host.Resolve<VersionService>(scope);
        var comparison = await versionSvc.CompareWithPreviousAsync(version2.Id, TestContext.CancellationToken);

        AssertStatus(comparison, "detection.yaml", VersionFileDiffStatus.Modified);
        AssertStatus(comparison, "rule.kql", VersionFileDiffStatus.Unchanged);
        AssertStatus(comparison, "tests/added.yaml", VersionFileDiffStatus.Added);
        AssertStatus(comparison, "fixtures/same.ndjson", VersionFileDiffStatus.Unchanged);
        Assert.HasCount(2, comparison.ChangedFiles);
    }

    [TestMethod]
    public async Task CompareWithPrevious_ModifiedTextFile_IncludesInlineDiffHunk()
    {
        var detectionId = await ConceiveDetectionAsync("compare-inline");
        await MergeQuickLabAsync(
            detectionId,
            "CHG-C3A",
            "Initial accepted content",
            new[]
            {
                ("rule.kql", DraftContentType.HuntingQuery, "SigninLogs\n| where ResultType == 0\n| take 1"),
            });
        var version2 = await MergeQuickLabAsync(
            detectionId,
            "CHG-C3B",
            "Second accepted content",
            new[]
            {
                ("rule.kql", DraftContentType.HuntingQuery, "SigninLogs\n| where ResultType != 0\n| summarize count()"),
            });

        using var scope = _host.CreateScope();
        var versionSvc = _host.Resolve<VersionService>(scope);
        var comparison = await versionSvc.CompareWithPreviousAsync(version2.Id, TestContext.CancellationToken);

        var file = comparison.Files.Single(file => file.LogicalPath == "rule.kql");
        Assert.AreEqual(VersionFileDiffStatus.Modified, file.Status);
        Assert.HasCount(1, file.Hunks);

        var hunk = file.Hunks.Single();
        Assert.AreEqual(1, hunk.BeforeStartLine);
        Assert.AreEqual(3, hunk.BeforeLineCount);
        Assert.AreEqual(1, hunk.AfterStartLine);
        Assert.AreEqual(3, hunk.AfterLineCount);
        CollectionAssert.AreEqual(
            new[]
            {
                VersionDiffLineType.Context,
                VersionDiffLineType.Added,
                VersionDiffLineType.Added,
                VersionDiffLineType.Removed,
                VersionDiffLineType.Removed,
            },
            hunk.Lines.Select(line => line.Type).ToArray());
        Assert.AreEqual("| where ResultType != 0", hunk.Lines[1].Text);
        Assert.AreEqual("| where ResultType == 0", hunk.Lines[3].Text);
    }

    [TestMethod]
    public async Task CompareWithPrevious_UnchangedTextFile_DoesNotCreateInlineHunks()
    {
        var detectionId = await ConceiveDetectionAsync("compare-inline-unchanged");
        await MergeQuickLabAsync(
            detectionId,
            "CHG-C4A",
            "Initial accepted content",
            new[]
            {
                ("rule.kql", DraftContentType.HuntingQuery, "SigninLogs | take 1"),
            });
        var version2 = await MergeQuickLabAsync(
            detectionId,
            "CHG-C4B",
            "Second accepted content",
            new[]
            {
                ("rule.kql", DraftContentType.HuntingQuery, "SigninLogs | take 1"),
                ("tests/added.yaml", DraftContentType.TestDefinition, "expectedRows: 1"),
            });

        using var scope = _host.CreateScope();
        var versionSvc = _host.Resolve<VersionService>(scope);
        var comparison = await versionSvc.CompareWithPreviousAsync(version2.Id, TestContext.CancellationToken);

        var unchanged = comparison.Files.Single(file => file.LogicalPath == "rule.kql");
        Assert.AreEqual(VersionFileDiffStatus.Unchanged, unchanged.Status);
        Assert.HasCount(0, unchanged.Hunks);

        var added = comparison.Files.Single(file => file.LogicalPath == "tests/added.yaml");
        Assert.AreEqual(VersionFileDiffStatus.Added, added.Status);
        Assert.HasCount(1, added.Hunks);
        Assert.AreEqual(VersionDiffLineType.Added, added.Hunks.Single().Lines.Single().Type);
    }

    [TestMethod]
    public async Task CompareWithPrevious_DistantTextChanges_CreatesSeparateContextHunks()
    {
        var detectionId = await ConceiveDetectionAsync("compare-inline-distant");
        var before = string.Join("\n", Enumerable.Range(1, 12).Select(index => $"row{index:00}"));
        var after = string.Join("\n", Enumerable.Range(1, 12).Select(index => index switch
        {
            2 => "changed-row02",
            11 => "changed-row11",
            _ => $"row{index:00}",
        }));

        await MergeQuickLabAsync(
            detectionId,
            "CHG-C5A",
            "Initial accepted content",
            new[]
            {
                ("rule.kql", DraftContentType.HuntingQuery, before),
            });
        var version2 = await MergeQuickLabAsync(
            detectionId,
            "CHG-C5B",
            "Second accepted content",
            new[]
            {
                ("rule.kql", DraftContentType.HuntingQuery, after),
            });

        using var scope = _host.CreateScope();
        var versionSvc = _host.Resolve<VersionService>(scope);
        var comparison = await versionSvc.CompareWithPreviousAsync(version2.Id, TestContext.CancellationToken);

        var file = comparison.Files.Single(file => file.LogicalPath == "rule.kql");
        Assert.AreEqual(VersionFileDiffStatus.Modified, file.Status);
        Assert.HasCount(2, file.Hunks);

        var firstHunk = file.Hunks[0];
        Assert.AreEqual(1, firstHunk.BeforeStartLine);
        Assert.AreEqual(1, firstHunk.AfterStartLine);
        Assert.IsTrue(firstHunk.Lines.Any(line => line.Text == "changed-row02"));
        Assert.IsFalse(firstHunk.Lines.Any(line => line.Text == "row06"));

        var secondHunk = file.Hunks[1];
        Assert.AreEqual(8, secondHunk.BeforeStartLine);
        Assert.AreEqual(8, secondHunk.AfterStartLine);
        Assert.IsTrue(secondHunk.Lines.Any(line => line.Text == "changed-row11"));
        Assert.IsFalse(secondHunk.Lines.Any(line => line.Text == "row06"));
    }

    private async Task<DetectionId> ConceiveDetectionAsync(string slug)
    {
        using var scope = _host.CreateScope();
        var svc = _host.Resolve<DetectionContentService>(scope);
        return (await svc.ConceiveAsync(slug, "Compare Test Detection", "", TestContext.CancellationToken)).Id;
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
            return await mergeSvc.MergeAsync(changeId, "Compare Tester", "compare@example.test", TestContext.CancellationToken);
        }
    }

    private static void AssertStatus(
        VersionComparison comparison,
        string logicalPath,
        VersionFileDiffStatus expectedStatus)
    {
        var file = comparison.Files.Single(file => file.LogicalPath == logicalPath);
        Assert.AreEqual(expectedStatus, file.Status);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    public TestContext TestContext { get; set; }
}
