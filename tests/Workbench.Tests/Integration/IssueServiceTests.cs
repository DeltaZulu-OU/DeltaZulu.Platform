using Workbench.Application.Services;

namespace Workbench.Tests.Integration;

[TestClass]
public sealed class IssueServiceTests : IDisposable
{
    private TestServiceProvider _host = null!;

    [TestInitialize]
    public void Setup() => _host = new TestServiceProvider();

    [TestCleanup]
    public void Teardown() => _host.Dispose();

    [TestMethod]
    public async Task CreateIssueAsync_PersistsIssue()
    {
        IssueId id;

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<IssueService>(scope);
            var issue = await svc.CreateIssueAsync("REQ-001", "Tune sign-in detection", IssueType.Request, ct: TestContext.CancellationToken);
            id = issue.Id;
            Assert.AreEqual(IssueStatus.Open, issue.Status);
        }

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<IssueService>(scope);
            var loaded = await svc.GetByIdAsync(id, TestContext.CancellationToken);
            Assert.IsNotNull(loaded);
            Assert.AreEqual("REQ-001", loaded.Key);
            Assert.AreEqual(IssueType.Request, loaded.Type);
        }
    }

    [TestMethod]
    public async Task CreateFromExternalCaseAsync_PersistsIssueWithFlowIntelRef()
    {
        IssueId id;

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<IssueService>(scope);
            var issue = await svc.CreateFromExternalCaseAsync(
                "CASE-001", "Investigate suspicious login",
                "flowintel", "FI-42", "https://flowintel.local/case/42", ct: TestContext.CancellationToken);
            id = issue.Id;
            Assert.AreEqual(IssueType.Case, issue.Type);
            Assert.IsNotNull(issue.ExternalCase);
            Assert.AreEqual("flowintel", issue.ExternalCase.System);
        }

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<IssueService>(scope);
            var loaded = await svc.GetByIdAsync(id, TestContext.CancellationToken);
            Assert.IsNotNull(loaded);
            Assert.IsNotNull(loaded.ExternalCase);
            Assert.AreEqual("FI-42", loaded.ExternalCase.ExternalId);
            Assert.AreEqual("https://flowintel.local/case/42", loaded.ExternalCase.Url);
        }
    }

    [TestMethod]
    public async Task CreateFromExternalCaseAsync_TheHive_PersistsRef()
    {
        IssueId id;

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<IssueService>(scope);
            var issue = await svc.CreateFromExternalCaseAsync(
                "CASE-002", "TheHive case",
                "thehive", "~987654", "https://thehive.local/cases/~987654/details", ct: TestContext.CancellationToken);
            id = issue.Id;
        }

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<IssueService>(scope);
            var loaded = await svc.GetByIdAsync(id, TestContext.CancellationToken);
            Assert.IsNotNull(loaded);
            Assert.IsNotNull(loaded.ExternalCase);
            Assert.AreEqual("thehive", loaded.ExternalCase.System);
            Assert.AreEqual("~987654", loaded.ExternalCase.ExternalId);
        }
    }

    [TestMethod]
    public async Task LinkExternalCaseAsync_AddsRefToExistingIssue()
    {
        IssueId id;

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<IssueService>(scope);
            var issue = await svc.CreateIssueAsync("REQ-010", "Request issue", IssueType.Request, ct: TestContext.CancellationToken);
            id = issue.Id;
        }

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<IssueService>(scope);
            await svc.LinkExternalCaseAsync(id, "flowintel", "FI-99", "https://flowintel.local/case/99", TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<IssueService>(scope);
            var loaded = await svc.GetByIdAsync(id, TestContext.CancellationToken);
            Assert.IsNotNull(loaded);
            Assert.IsNotNull(loaded.ExternalCase);
            Assert.AreEqual("FI-99", loaded.ExternalCase.ExternalId);
        }
    }

    [TestMethod]
    public async Task ListAsync_ReturnsBothIssueTypes()
    {
        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<IssueService>(scope);
            await svc.CreateIssueAsync("REQ-020", "A request", IssueType.Request, ct: TestContext.CancellationToken);
            await svc.CreateFromExternalCaseAsync("CASE-020", "A case", "flowintel", "FI-1", ct: TestContext.CancellationToken);
        }

        using (var scope = _host.CreateScope())
        {
            var svc = _host.Resolve<IssueService>(scope);
            var list = await svc.ListAsync(TestContext.CancellationToken);
            Assert.HasCount(2, list);
            Assert.Contains(i => i.Type == IssueType.Request, list);
            Assert.Contains(i => i.Type == IssueType.Case, list);
        }
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    public TestContext TestContext { get; set; }
}
