using DeltaZulu.Platform.Application.AgentManagement.Security;
using DeltaZulu.Platform.Application.AgentManagement.Services;
using DeltaZulu.Platform.Domain.AgentManagement.Enrollment;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

namespace DeltaZulu.Platform.Tests.AgentManagement.Application;

[TestClass]
public sealed class AgentAuthenticationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Resolve_ValidSecret_ReturnsAgentId()
    {
        var credentials = new FakeAgentCredentialRepository();
        var agentId = AgentId.New();
        var secret = AgentSecrets.GenerateAgentSecret();
        credentials.Add(AgentCredential.Issue(agentId, AgentSecrets.Hash(secret), Now));

        var resolved = await new AgentAuthenticationService(credentials).ResolveAgentIdAsync(secret, TestContext.CancellationToken);

        Assert.AreEqual(agentId, resolved);
    }

    [TestMethod]
    public async Task Resolve_UnknownSecret_ReturnsNull()
    {
        var service = new AgentAuthenticationService(new FakeAgentCredentialRepository());

        Assert.IsNull(await service.ResolveAgentIdAsync("dz-as-unknown", TestContext.CancellationToken));
        Assert.IsNull(await service.ResolveAgentIdAsync("", TestContext.CancellationToken));
        Assert.IsNull(await service.ResolveAgentIdAsync(null, TestContext.CancellationToken));
    }

    public TestContext TestContext { get; set; }
}
