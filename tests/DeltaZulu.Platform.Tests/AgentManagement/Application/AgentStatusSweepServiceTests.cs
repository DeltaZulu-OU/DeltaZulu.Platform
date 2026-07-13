using DeltaZulu.Platform.Application.AgentManagement.Services;
using DeltaZulu.Platform.Domain.AgentManagement.Agents;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

namespace DeltaZulu.Platform.Tests.AgentManagement.Application;

[TestClass]
public sealed class AgentStatusSweepServiceTests
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan OfflineAfter = TimeSpan.FromMinutes(60);

    private readonly TestClock _clock = new();
    private readonly FakeAgentRepository _agents = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private AgentStatusSweepService CreateService() => new(_agents, _unitOfWork, _clock);

    private Agent NewHeartbeatedAgent()
    {
        var agent = Agent.Enroll(AgentId.New(), TenantId.Default, $"host-{Guid.NewGuid():N}",
            ResourcePlatform.Linux, _clock.Now);
        agent.RecordHeartbeat("1.0", _clock.Now);
        _agents.Add(agent);
        return agent;
    }

    [TestMethod]
    public async Task Sweep_RecentAgent_StaysOnline()
    {
        var agent = NewHeartbeatedAgent();
        _clock.Advance(TimeSpan.FromMinutes(5));

        var transitions = await CreateService().SweepAsync(TenantId.Default, StaleAfter, OfflineAfter);

        Assert.AreEqual(0, transitions);
        Assert.AreEqual(AgentStatus.Online, agent.Status);
    }

    [TestMethod]
    public async Task Sweep_AgentPastStaleThreshold_BecomesStale()
    {
        var agent = NewHeartbeatedAgent();
        _clock.Advance(TimeSpan.FromMinutes(20));

        var transitions = await CreateService().SweepAsync(TenantId.Default, StaleAfter, OfflineAfter);

        Assert.AreEqual(1, transitions);
        Assert.AreEqual(AgentStatus.Stale, agent.Status);
    }

    [TestMethod]
    public async Task Sweep_AgentPastOfflineThreshold_BecomesOffline()
    {
        var agent = NewHeartbeatedAgent();
        _clock.Advance(TimeSpan.FromMinutes(90));

        var transitions = await CreateService().SweepAsync(TenantId.Default, StaleAfter, OfflineAfter);

        Assert.AreEqual(1, transitions);
        Assert.AreEqual(AgentStatus.Offline, agent.Status);
    }

    [TestMethod]
    public async Task Sweep_NeverHeartbeatedAgent_UsesCreatedAt()
    {
        var agent = Agent.Enroll(AgentId.New(), TenantId.Default, "silent-host",
            ResourcePlatform.Windows, _clock.Now);
        _agents.Add(agent);
        _clock.Advance(TimeSpan.FromMinutes(90));

        await CreateService().SweepAsync(TenantId.Default, StaleAfter, OfflineAfter);

        Assert.AreEqual(AgentStatus.Offline, agent.Status);
    }

    [TestMethod]
    public async Task Sweep_AlreadyOfflineAgent_IsNotTouchedAgain()
    {
        var agent = NewHeartbeatedAgent();
        _clock.Advance(TimeSpan.FromMinutes(90));
        var service = CreateService();
        await service.SweepAsync(TenantId.Default, StaleAfter, OfflineAfter);

        var secondPass = await service.SweepAsync(TenantId.Default, StaleAfter, OfflineAfter);

        Assert.AreEqual(0, secondPass);
        Assert.AreEqual(AgentStatus.Offline, agent.Status);
    }
}
