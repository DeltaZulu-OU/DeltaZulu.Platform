namespace DeltaZulu.Platform.Tests.Workbench.Infrastructure;

[TestClass]
public sealed class InMemoryContentStoreTests
{
    [TestMethod]
    public async Task CommitAndRead_TextFile_RoundTrips()
    {
        var store = new InMemoryContentStore();

        var commit = new CommitRequest(
            "Initial commit",
            "Test User", "test@test.com",
            [new ContentFile("detections/test/rule.kql", "SigninLogs | take 1")]);

        var result = await store.CommitAsync(commit, TestContext.CancellationToken);

        Assert.IsNotNull(result.CommitSha);
        Assert.AreNotEqual(string.Empty, result.CommitSha);

        var file = await store.GetFileAsync("detections/test/rule.kql", TestContext.CancellationToken);
        Assert.IsNotNull(file);
        Assert.AreEqual("SigninLogs | take 1", file.Content);
        Assert.IsFalse(file.IsBinary);
    }

    [TestMethod]
    public async Task CommitAndRead_BinaryFile_PreservesBinaryFlag()
    {
        var store = new InMemoryContentStore();

        var b64 = Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG magic
        var commit = new CommitRequest(
            "Add image",
            "Test User", "test@test.com",
            [new ContentFile("detections/test/notes/assets/img.png", b64, isBinary: true)]);

        await store.CommitAsync(commit, TestContext.CancellationToken);

        var file = await store.GetFileAsync("detections/test/notes/assets/img.png", TestContext.CancellationToken);
        Assert.IsNotNull(file);
        Assert.IsTrue(file.IsBinary);
        Assert.AreEqual(b64, file.Content);
    }

    [TestMethod]
    public async Task ListFiles_ReturnsFilesUnderPrefix()
    {
        var store = new InMemoryContentStore();

        await store.CommitAsync(new CommitRequest("c1", "u", "e@e.com", [
            new ContentFile("detections/slug-a/rule.kql", "a"),
            new ContentFile("detections/slug-a/detection.yaml", "b"),
            new ContentFile("detections/slug-b/rule.kql", "c"),
        ]), TestContext.CancellationToken);

        var files = await store.ListFilesAsync("detections/slug-a", TestContext.CancellationToken);
        Assert.HasCount(2, files);
        Assert.IsTrue(files.All(f => f.RepositoryPath.StartsWith("detections/slug-a/", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task Exists_ReturnsTrueForExistingPath()
    {
        var store = new InMemoryContentStore();
        await store.CommitAsync(new CommitRequest("c1", "u", "e@e.com",
            [new ContentFile("detections/test/rule.kql", "x")]), TestContext.CancellationToken);

        Assert.IsTrue(await store.ExistsAsync("detections/test/rule.kql", TestContext.CancellationToken));
        Assert.IsFalse(await store.ExistsAsync("detections/test/missing.yaml", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GetFileAtCommit_ReturnsSnapshotNotHead()
    {
        var store = new InMemoryContentStore();

        var r1 = await store.CommitAsync(new CommitRequest("c1", "u", "e@e.com",
            [new ContentFile("detections/test/rule.kql", "version-1")]), TestContext.CancellationToken);

        await store.CommitAsync(new CommitRequest("c2", "u", "e@e.com",
            [new ContentFile("detections/test/rule.kql", "version-2")]), TestContext.CancellationToken);

        // HEAD has version-2.
        var head = await store.GetFileAsync("detections/test/rule.kql", TestContext.CancellationToken);
        Assert.AreEqual("version-2", head!.Content);

        // Commit 1 snapshot has version-1.
        var snapshot = await store.GetFileAtCommitAsync("detections/test/rule.kql", r1.CommitSha, TestContext.CancellationToken);
        Assert.AreEqual("version-1", snapshot!.Content);
    }

    [TestMethod]
    public async Task CommitWithDeletes_RemovesFiles()
    {
        var store = new InMemoryContentStore();

        await store.CommitAsync(new CommitRequest("c1", "u", "e@e.com", [
            new ContentFile("detections/test/rule.kql", "x"),
            new ContentFile("detections/test/old.yaml", "y"),
        ]), TestContext.CancellationToken);

        await store.CommitAsync(new CommitRequest("c2", "u", "e@e.com",
            filesToWrite: [],
            pathsToDelete: ["detections/test/old.yaml"]), TestContext.CancellationToken);

        Assert.IsTrue(await store.ExistsAsync("detections/test/rule.kql", TestContext.CancellationToken));
        Assert.IsFalse(await store.ExistsAsync("detections/test/old.yaml", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GetHeadCommitSha_EmptyStore_ReturnsNull()
    {
        var store = new InMemoryContentStore();
        Assert.IsNull(await store.GetHeadCommitShaAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GetHeadCommitSha_AfterCommit_ReturnsSha()
    {
        var store = new InMemoryContentStore();
        var r = await store.CommitAsync(new CommitRequest("c1", "u", "e@e.com",
            [new ContentFile("f.txt", "x")]), TestContext.CancellationToken);

        Assert.AreEqual(r.CommitSha, await store.GetHeadCommitShaAsync(TestContext.CancellationToken));
    }

    public TestContext TestContext { get; set; }
}