using DeltaZulu.Platform.Data.Git;
using DeltaZulu.Platform.Domain.Workbench.Contracts;
using LibGit2Sharp;
using Microsoft.Extensions.Options;

namespace DeltaZulu.Platform.Tests.Workbench.Infrastructure;

[TestClass]
public sealed class GitAcceptedContentStoreTests
{
    [TestMethod]
    public async Task CommitAndRead_TextFile_RoundTripsThroughGit()
    {
        using var temp = new TemporaryDirectory();
        var store = CreateStore(temp.Path);

        var result = await store.CommitAsync(new CommitRequest(
            "Initial detection",
            "Test User",
            "test@example.com",
            [new ContentFile("detections/test/rule.kql", "SigninLogs | take 1")]), TestContext.CancellationToken);

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.CommitSha));
        Assert.AreEqual(result.CommitSha, await store.GetHeadCommitShaAsync(TestContext.CancellationToken));

        var file = await store.GetFileAsync("detections/test/rule.kql", TestContext.CancellationToken);
        Assert.IsNotNull(file);
        Assert.AreEqual("SigninLogs | take 1", file.Content);
        Assert.IsFalse(file.IsBinary);

        var rawBytes = await File.ReadAllBytesAsync(
            Path.Combine(temp.Path, "detections", "test", "rule.kql"),
            TestContext.CancellationToken);
        var utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF };
        Assert.IsFalse(rawBytes.Length >= utf8Bom.Length && rawBytes.Take(utf8Bom.Length).SequenceEqual(utf8Bom));
    }

    [TestMethod]
    public async Task GetFileAtCommit_ReturnsHistoricalSnapshot()
    {
        using var temp = new TemporaryDirectory();
        var store = CreateStore(temp.Path);

        var first = await store.CommitAsync(new CommitRequest(
            "v1",
            "Test User",
            "test@example.com",
            [new ContentFile("detections/test/rule.kql", "version-1")]), TestContext.CancellationToken);

        await store.CommitAsync(new CommitRequest(
            "v2",
            "Test User",
            "test@example.com",
            [new ContentFile("detections/test/rule.kql", "version-2")]), TestContext.CancellationToken);

        var head = await store.GetFileAsync("detections/test/rule.kql", TestContext.CancellationToken);
        var historical = await store.GetFileAtCommitAsync("detections/test/rule.kql", first.CommitSha, TestContext.CancellationToken);

        Assert.AreEqual("version-2", head!.Content);
        Assert.AreEqual("version-1", historical!.Content);
    }

    [TestMethod]
    public async Task CommitWithDeletes_RemovesFileFromHead()
    {
        using var temp = new TemporaryDirectory();
        var store = CreateStore(temp.Path);

        await store.CommitAsync(new CommitRequest(
            "create files",
            "Test User",
            "test@example.com",
            [
                new ContentFile("detections/test/rule.kql", "query"),
                new ContentFile("detections/test/old.yaml", "old")
            ]), TestContext.CancellationToken);

        await store.CommitAsync(new CommitRequest(
            "delete stale file",
            "Test User",
            "test@example.com",
            filesToWrite: [],
            pathsToDelete: ["detections/test/old.yaml"]), TestContext.CancellationToken);

        Assert.IsTrue(await store.ExistsAsync("detections/test/rule.kql", TestContext.CancellationToken));
        Assert.IsFalse(await store.ExistsAsync("detections/test/old.yaml", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task CommitAndRead_BinaryFile_RoundTripsAsBase64()
    {
        using var temp = new TemporaryDirectory();
        var store = CreateStore(temp.Path);
        var content = Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        await store.CommitAsync(new CommitRequest(
            "add image",
            "Test User",
            "test@example.com",
            [new ContentFile("detections/test/notes/assets/image.png", content, isBinary: true)]), TestContext.CancellationToken);

        var file = await store.GetFileAsync("detections/test/notes/assets/image.png", TestContext.CancellationToken);

        Assert.IsNotNull(file);
        Assert.IsTrue(file.IsBinary);
        Assert.AreEqual(content, file.Content);
    }

    [TestMethod]
    public async Task ListFiles_ReturnsOnlyFilesUnderPrefix()
    {
        using var temp = new TemporaryDirectory();
        var store = CreateStore(temp.Path);

        await store.CommitAsync(new CommitRequest(
            "create files",
            "Test User",
            "test@example.com",
            [
                new ContentFile("detections/one/rule.kql", "one"),
                new ContentFile("detections/one/detection.yaml", "metadata"),
                new ContentFile("detections/two/rule.kql", "two")
            ]), TestContext.CancellationToken);

        var files = await store.ListFilesAsync("detections/one", TestContext.CancellationToken);

        Assert.HasCount(2, files);
        Assert.IsTrue(files.All(file => file.RepositoryPath.StartsWith("detections/one/", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ListFilesAtCommit_ReturnsHistoricalFilesUnderPrefix()
    {
        using var temp = new TemporaryDirectory();
        var store = CreateStore(temp.Path);

        var first = await store.CommitAsync(new CommitRequest(
            "v1",
            "Test User",
            "test@example.com",
            [
                new ContentFile("detections/test/rule.kql", "version-1"),
                new ContentFile("detections/test/detection.yaml", "metadata-v1"),
                new ContentFile("detections/other/rule.kql", "other")
            ]), TestContext.CancellationToken);

        await store.CommitAsync(new CommitRequest(
            "v2",
            "Test User",
            "test@example.com",
            [new ContentFile("detections/test/rule.kql", "version-2")]), TestContext.CancellationToken);

        var files = await store.ListFilesAtCommitAsync("detections/test", first.CommitSha, TestContext.CancellationToken);

        Assert.HasCount(2, files);
        Assert.IsTrue(files.All(file => file.RepositoryPath.StartsWith("detections/test/", StringComparison.Ordinal)));
        Assert.AreEqual("version-1", files.Single(file => file.RepositoryPath.EndsWith("rule.kql", StringComparison.Ordinal)).Content);
    }

    [TestMethod]
    public async Task CommitExistsAsync_ReportsKnownAndUnknownCommits()
    {
        using var temp = new TemporaryDirectory();
        var store = CreateStore(temp.Path);

        var result = await store.CommitAsync(new CommitRequest(
            "create file",
            "Test User",
            "test@example.com",
            [new ContentFile("detections/test/rule.kql", "query")]), TestContext.CancellationToken);

        Assert.IsTrue(await store.CommitExistsAsync(result.CommitSha, TestContext.CancellationToken));
        Assert.IsFalse(await store.CommitExistsAsync("0000000000000000000000000000000000000000", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task CommitAsync_RejectsPathTraversal()
    {
        using var temp = new TemporaryDirectory();
        var store = CreateStore(temp.Path);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.CommitAsync(new CommitRequest(
            "bad path",
            "Test User",
            "test@example.com",
            [new ContentFile("../outside.txt", "bad")]), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task WebHostStoreCanBeReadByPlainGitRepository()
    {
        using var temp = new TemporaryDirectory();
        var store = CreateStore(temp.Path);

        var result = await store.CommitAsync(new CommitRequest(
            "plain git verification",
            "Test User",
            "test@example.com",
            [new ContentFile("detections/test/detection.yaml", "name: test")]), TestContext.CancellationToken);

        using var repository = new Repository(temp.Path);
        Assert.AreEqual(result.CommitSha, repository.Head.Tip.Sha);
        Assert.IsNotNull(repository.Head.Tip.Tree["detections/test/detection.yaml"]);
    }

    public TestContext TestContext { get; set; }

    private static GitAcceptedContentStore CreateStore(string repositoryPath)
        => new(Options.Create(new GitAcceptedContentStoreOptions { RepositoryPath = repositoryPath }));

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "workbench-git-store-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}