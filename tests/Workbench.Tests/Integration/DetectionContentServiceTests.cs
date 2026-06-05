using Workbench.Application.Services;

namespace Workbench.Tests.Integration;

[TestClass]
public sealed class DetectionContentServiceTests : IDisposable
{
    private TestServiceProvider _host = null!;

    [TestInitialize]
    public void Setup() => _host = new TestServiceProvider();

    [TestCleanup]
    public void Teardown() => _host.Dispose();

    [TestMethod]
    public async Task ConceiveAsync_PersistsDetection_And_ListReturnsIt()
    {
        DetectionId id;

        // Scope 1: create
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<DetectionContentService>(scope);
            var det = await svc.ConceiveAsync("anomalous-sign-in", "Anomalous Sign-In", "Detects unusual sign-in patterns.", TestContext.CancellationToken);
            id = det.Id;
            Assert.AreEqual("anomalous-sign-in", det.Slug);
            Assert.AreEqual(DetectionLifecycle.Conceived, det.Lifecycle);
        }

        // Scope 2: read back from a fresh DbContext
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<DetectionContentService>(scope);
            var list = await svc.ListAsync(TestContext.CancellationToken);
            Assert.HasCount(1, list);
            Assert.AreEqual(id, list[0].Id);
            Assert.AreEqual("anomalous-sign-in", list[0].Slug);
        }
    }

    [TestMethod]
    public async Task ConceiveAsync_DuplicateSlug_Throws()
    {
        using var scope = _host.CreateScope();
        var svc = _host.Resolve<DetectionContentService>(scope);

        await svc.ConceiveAsync("dup-slug", "First", "", TestContext.CancellationToken);

        var ex = await Assert.ThrowsExactlyAsync<DomainException>(
            () => svc.ConceiveAsync("dup-slug", "Second", "", TestContext.CancellationToken));
        Assert.AreEqual("detection.slug_duplicate", ex.Code);
    }

    [TestMethod]
    public async Task RenameAsync_UpdatesTitle_Persists()
    {
        DetectionId id;
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<DetectionContentService>(scope);
            var det = await svc.ConceiveAsync("rename-me", "Old Title", "", TestContext.CancellationToken);
            id = det.Id;
        }

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<DetectionContentService>(scope);
            var renamed = await svc.RenameAsync(id, "New Title", TestContext.CancellationToken);
            Assert.AreEqual("New Title", renamed.Title);
        }

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<DetectionContentService>(scope);
            var det = await svc.GetByIdAsync(id, TestContext.CancellationToken);
            Assert.IsNotNull(det);
            Assert.AreEqual("New Title", det.Title);
        }
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    public TestContext TestContext { get; set; }
}
