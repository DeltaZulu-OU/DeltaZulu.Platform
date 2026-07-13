using DeltaZulu.Platform.Application.AgentManagement.Security;
using DeltaZulu.Platform.Application.AgentManagement.Services;
using DeltaZulu.Platform.Domain.AgentManagement.Enrollment;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Tests.AgentManagement.Application;

[TestClass]
public sealed class AgentEnrollmentServiceTests
{
    private readonly TestClock _clock = new();
    private readonly FakeEnrollmentTokenRepository _tokens = new();
    private readonly FakeAgentRepository _agents = new();
    private readonly FakeAgentCredentialRepository _credentials = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private AgentEnrollmentService CreateService() =>
        new(_tokens, _agents, _credentials, _unitOfWork, _clock);

    private string IssueToken(int maxUses = 5, TimeSpan? ttl = null)
    {
        var plaintext = AgentSecrets.GenerateEnrollmentToken();
        _tokens.Add(EnrollmentToken.Create(
            EnrollmentTokenId.New(), TenantId.Default, "test", AgentSecrets.Hash(plaintext),
            _clock.Now.Add(ttl ?? TimeSpan.FromHours(1)), maxUses, null, _clock.Now));
        return plaintext;
    }

    [TestMethod]
    public async Task Enroll_CreatesAgentAndCredential()
    {
        var token = IssueToken();

        var result = await CreateService().EnrollAsync(
            token, "server-01", ResourcePlatform.Linux, "1.2.3", ["web"]);

        Assert.AreEqual("server-01", result.Agent.Hostname);
        Assert.StartsWith(AgentSecrets.AgentSecretPrefix, result.AgentSecret);
        Assert.HasCount(1, _agents.Agents);
        Assert.HasCount(1, _credentials.Credentials);
        Assert.AreEqual(AgentSecrets.Hash(result.AgentSecret),
            _credentials.Credentials[result.Agent.Id].SecretHash);
        Assert.AreEqual(1, _tokens.Tokens.Values.Single().UseCount);
    }

    [TestMethod]
    public async Task Enroll_UnknownToken_Throws()
    {
        var ex = await Assert.ThrowsExactlyAsync<DomainException>(() =>
            CreateService().EnrollAsync("dz-et-bogus", "host", ResourcePlatform.Windows));
        Assert.AreEqual("enrollmenttoken.invalid", ex.Code);
    }

    [TestMethod]
    public async Task Enroll_ExpiredToken_Throws()
    {
        var token = IssueToken(ttl: TimeSpan.FromMinutes(5));
        _clock.Advance(TimeSpan.FromMinutes(10));

        var ex = await Assert.ThrowsExactlyAsync<DomainException>(() =>
            CreateService().EnrollAsync(token, "host", ResourcePlatform.Windows));
        Assert.AreEqual("enrollmenttoken.expired", ex.Code);
    }

    [TestMethod]
    public async Task Enroll_RevokedToken_Throws()
    {
        var token = IssueToken();
        _tokens.Tokens.Values.Single().Revoke(_clock.Now);

        var ex = await Assert.ThrowsExactlyAsync<DomainException>(() =>
            CreateService().EnrollAsync(token, "host", ResourcePlatform.Windows));
        Assert.AreEqual("enrollmenttoken.revoked", ex.Code);
    }

    [TestMethod]
    public async Task Enroll_ExhaustedToken_Throws()
    {
        var token = IssueToken(maxUses: 1);
        var service = CreateService();
        await service.EnrollAsync(token, "host-1", ResourcePlatform.Windows);

        var ex = await Assert.ThrowsExactlyAsync<DomainException>(() =>
            service.EnrollAsync(token, "host-2", ResourcePlatform.Windows));
        Assert.AreEqual("enrollmenttoken.exhausted", ex.Code);
    }

    [TestMethod]
    public async Task Enroll_SameHostname_ReusesAgentAndRotatesSecret()
    {
        var token = IssueToken();
        var service = CreateService();

        var first = await service.EnrollAsync(token, "server-01", ResourcePlatform.Linux);
        var second = await service.EnrollAsync(token, "server-01", ResourcePlatform.Linux);

        Assert.AreEqual(first.Agent.Id, second.Agent.Id);
        Assert.AreNotEqual(first.AgentSecret, second.AgentSecret);
        Assert.HasCount(1, _agents.Agents);
        Assert.HasCount(1, _credentials.Credentials);
        Assert.IsNotNull(_credentials.Credentials[first.Agent.Id].RotatedAt);
        Assert.AreEqual(AgentSecrets.Hash(second.AgentSecret),
            _credentials.Credentials[first.Agent.Id].SecretHash);
    }
}
